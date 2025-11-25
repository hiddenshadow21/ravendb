using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features.Authentication;

namespace Raven.Server.WindowsAuth;

public class RavenWindowsAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RavenServer _server;

    public RavenWindowsAuthMiddleware(RequestDelegate next, RavenServer server)
    {
        _next = next;
        _server = server;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 1. Check if we already have a Windows User (from Negotiate Middleware)
        var existingWindowsUser = context.User;

        // 2. Retrieve the RavenDB auth feature (created by HttpsConnectionMiddleware on HTTPS)
        var authFeature = context.Features.Get<IHttpAuthenticationFeature>() as RavenServer.AuthenticateConnection;

        // 3. If running on HTTP or no Cert, the feature might be the default one or null.
        if (authFeature == null)
        {
            authFeature = new RavenServer.AuthenticateConnection(_server.TwoFactor);
            authFeature.Status = RavenServer.AuthenticationStatus.NoCertificateProvided;

            // Restore the Windows User into our new feature
            if (existingWindowsUser != null)
            {
                ((IHttpAuthenticationFeature)authFeature).User = existingWindowsUser;
            }

            context.Features.Set<IHttpAuthenticationFeature>(authFeature);
        }

        // 4. Proceed with logic (Cert vs Windows check)
        // If we have no cert provided, we check for Windows Auth
        if (_server.Configuration.Security.WindowsAuthEnabled && 
            (authFeature.Status == RavenServer.AuthenticationStatus.NoCertificateProvided ||
             authFeature.Status == RavenServer.AuthenticationStatus.None))
        {
            // If the user is NOT authenticated via Windows, trigger the Challenge
            if (context.User?.Identity?.IsAuthenticated != true)
            {
                await context.ChallengeAsync(NegotiateDefaults.AuthenticationScheme);
                // CRITICAL: We return here to stop the pipeline. 
                // The browser will receive 401 and show the login popup.
                return; 
            }
            
            // If we are here, Windows Auth succeeded.
            // We can optionally set the RavenDB status here if we want the logic centralized,
            // or leave it to the RequestHandler/Router.
            // authFeature.Status = RavenServer.AuthenticationStatus.ClusterAdmin;
        }

        // 5. Call the next step in the pipeline
        await _next(context);
    }
}
