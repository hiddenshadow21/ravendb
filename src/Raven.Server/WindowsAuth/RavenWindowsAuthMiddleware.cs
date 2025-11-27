using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Features.Authentication;

namespace Raven.Server.WindowsAuth;

public class RavenWindowsAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RavenServer _server;
    private const string RetryCookieName = "Raven-Auth-Retries";
    private const int MaxRetries = 3;

    public RavenWindowsAuthMiddleware(RequestDelegate next, RavenServer server)
    {
        _next = next;
        _server = server;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_server.Configuration.Security.WindowsAuthEnabled == false)
        {
            await _next(context);
            return;
        }

        // 1. Capture existing Windows User (if authentication succeeded)
        var existingWindowsUser = context.User;

        // 2. Setup RavenDB Auth Feature (bridge)
        var authFeature = context.Features.Get<IHttpAuthenticationFeature>() as RavenServer.AuthenticateConnection;
        if (authFeature == null)
        {
            authFeature = new RavenServer.AuthenticateConnection(_server.TwoFactor);
            authFeature.Status = RavenServer.AuthenticationStatus.NoCertificateProvided;

            if (existingWindowsUser != null)
            {
                authFeature.User = existingWindowsUser;
            }

            context.Features.Set<IHttpAuthenticationFeature>(authFeature);
        }
        else
        {
            if (authFeature.Certificate != null)
            {
                await _next(context);
                return;
            }
        }

        // 3. Logic: Decide whether to Allow, Challenge, or Give Up (Show Error Page)
        
        // CASE A: User IS Authenticated
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            // Clear the retry cookie so they don't get blocked next time
            if (context.Request.Cookies.ContainsKey(RetryCookieName))
            {
                context.Response.Cookies.Delete(RetryCookieName);
            }

            // Map the Windows User to RavenDB permissions
            var connectionInfo = context.Features.Get<IHttpConnectionFeature>();
            authFeature = _server.AuthenticateConnectionWindowsUser(existingWindowsUser, connectionInfo);
            context.Features.Set<IHttpAuthenticationFeature>(authFeature);
        }
        // CASE B: User is NOT Authenticated (Anonymous)
        else
        {
            // Check how many times we have tried to challenge this specific client
            int retryCount = 0;
            if (context.Request.Cookies.TryGetValue(RetryCookieName, out string retryVal))
            {
                int.TryParse(retryVal, out retryCount);
            }

            // If we haven't reached the limit yet, FORCE the popup
            if (retryCount < MaxRetries)
            {
                // Increment cookie (Expires in 1 minute to reset state automatically)
                context.Response.Cookies.Append(RetryCookieName, (retryCount + 1).ToString(), 
                    new CookieOptions { MaxAge = TimeSpan.FromMinutes(1) });

                await context.ChallengeAsync(NegotiateDefaults.AuthenticationScheme);
                return; // Stop pipeline here, browser will show login dialog
            }
            
            // CASE C: Retry Limit Exceeded
            // We do NOT call ChallengeAsync. We proceed.
            // context.User is null. authFeature.Status is NoCertificateProvided.
            // The downstream RequestRouter will catch this and serve your custom Error Page.
        }

        // 4. Proceed to RequestRouter (or next middleware)
        await _next(context);
    }
}
