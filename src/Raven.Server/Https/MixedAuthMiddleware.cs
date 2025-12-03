using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Features.Authentication;

namespace Raven.Server.Https;

public class MixedAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RavenServer _server;
    
    // Cookie to track failed Windows Auth attempts
    private const string RetryCookieName = "Raven-Auth-Retries";
    private const int MaxRetries = 3;

    // Cookie to remember the user wants to use Certificates
    private const string CertPreferenceCookieName = "Raven-Prefer-Cert";

    public MixedAuthMiddleware(RequestDelegate next, RavenServer server)
    {
        _next = next;
        _server = server;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        bool windowsAuthEnabled = _server.Configuration.Security.WindowsAuthEnabled;
        if (windowsAuthEnabled == false)
        {
            throw new InvalidOperationException("Windows Authentication is not enabled on this server.");
        }
        
        // 1. Headers Check (External Load Balancers / Reverse Proxies)
        var certHeader = GetHeader(context, "X-Certificate");
        if (string.IsNullOrEmpty(certHeader) == false)
        {
            await TryAuthorizeWithCertificate(context);
            return;
        }
        
        var windowsAuthHeader = GetHeader(context, "X-WindowsAuth");
        if (string.IsNullOrEmpty(windowsAuthHeader) == false)
        {
            await TryAuthorizeWithWindowsAuth(context);
            return;
        }

        if (_server.Configuration.Security.PrioritizeWindowsAuth)
        {
            // We should use Certificate Auth if ANY of these are true:
            // A. The user explicitly asked via URL (?askForCertificate=true)
            // B. We previously set a cookie saying they prefer certificates
            // C. The TLS connection already has a certificate attached (Reuse)

            bool explicitRequest = context.Request.Query.ContainsKey("askForCertificate");
            bool hasPreferenceCookie = context.Request.Cookies.ContainsKey(CertPreferenceCookieName);
            bool hasCertOnConnection = context.Connection.ClientCertificate != null;

            if (explicitRequest || hasPreferenceCookie || hasCertOnConnection)
            {
                await TryAuthorizeWithCertificate(context);
                return;
            }
        }
        else
        {
            var authFeature = context.Features.Get<IHttpAuthenticationFeature>() as RavenServer.AuthenticateConnection;
            // certificate was provided during tls handshake
            if (authFeature?.Certificate != null)
            {
                await _next(context);
                return;
            }
        }
        
        await TryAuthorizeWithWindowsAuth(context);
    }

    private async Task TryAuthorizeWithWindowsAuth(HttpContext context)
    {
        // 1. Capture existing Windows User
        var existingWindowsUser = context.User;

        // 2. Setup RavenDB Auth Feature
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

        // 3. Authenticate or Challenge
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            // Success
            if (context.Request.Cookies.ContainsKey(RetryCookieName))
                context.Response.Cookies.Delete(RetryCookieName);

            // Clear the Cert Preference since they are using Windows Auth now
            if (context.Request.Cookies.ContainsKey(CertPreferenceCookieName))
                context.Response.Cookies.Delete(CertPreferenceCookieName);

            var connectionInfo = context.Features.Get<IHttpConnectionFeature>();
            authFeature = _server.AuthenticateConnectionWindowsUser(existingWindowsUser, connectionInfo);
            context.Features.Set<IHttpAuthenticationFeature>(authFeature);
        }
        else
        {
            // Failure / Anonymous
            int retryCount = 0;
            if (context.Request.Cookies.TryGetValue(RetryCookieName, out string retryVal))
            {
                int.TryParse(retryVal, out retryCount);
            }

            if (retryCount < MaxRetries)
            {
                context.Response.Cookies.Append(RetryCookieName, (retryCount + 1).ToString(),
                    new CookieOptions { MaxAge = TimeSpan.FromMinutes(1) });

                await context.ChallengeAsync(NegotiateDefaults.AuthenticationScheme);
                return; // Stop pipeline
            }
            
            // If retries exceeded, fall through to RequestRouter (which shows error page)
        }

        await _next(context);
    }

    private async Task TryAuthorizeWithCertificate(HttpContext context)
    {
        var tlsConnectionFeature = context.Features.Get<ITlsConnectionFeature>();
        X509Certificate2 certificate = null;

        if (tlsConnectionFeature != null)
        {
            // If we don't have a cert yet, try to ask for one (Renegotiation)
            if (tlsConnectionFeature.ClientCertificate == null)
            {
                try
                {
                    // This triggers the browser popup
                    certificate = await tlsConnectionFeature.GetClientCertificateAsync(context.RequestAborted);
                }
                catch (IOException)
                {
                    // Browser closed connection (User Cancelled). Stop processing.
                    return;
                }
                catch (Exception)
                {
                    // Other SSL error, treat as no cert
                }
            }
            else
            {
                // Connection reused, cert already present
                certificate = HttpsConnectionMiddleware.ConvertToX509Certificate2(tlsConnectionFeature.ClientCertificate);
            }
        }

        certificate = RavenServer.GetCertificateForAuthorization(certificate);

        var httpConnectionFeature = context.Features.Get<IHttpConnectionFeature>();
        var authenticationStatus = _server.AuthenticateConnectionCertificate(certificate, httpConnectionFeature);

        // [LOGIC] Manage the Preference Cookie
        if (authenticationStatus.Status != RavenServer.AuthenticationStatus.NoCertificateProvided &&
            authenticationStatus.Status != RavenServer.AuthenticationStatus.None)
        {
            // User provided a certificate (even if unfamiliar). Remember this preference.
            // This ensures the next request (redirect) comes back here instead of Windows Auth.
            context.Response.Cookies.Append(CertPreferenceCookieName, "true", new CookieOptions 
            { 
                Path = "/", 
                HttpOnly = true, 
                Secure = true, 
                Expires = DateTimeOffset.UtcNow.AddDays(7) 
            });
        }
        else
        {
            // User cancelled or provided nothing. Clear preference so they can try Windows Auth later if they want.
            if (context.Request.Cookies.ContainsKey(CertPreferenceCookieName))
            {
                context.Response.Cookies.Delete(CertPreferenceCookieName);
            }
        }

        context.Features.Set<IHttpAuthenticationFeature>(authenticationStatus);
        await _next(context);
    }

    public string GetHeader(HttpContext context, string key)
    {
        var request = context.Request;
        if (request.Headers.TryGetValue(key, out var values))
            return values.FirstOrDefault();
        return null;
    }
}
