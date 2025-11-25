Separate Windows Authentication endpoint alongside RavenDB (same cert, different port)

Summary
- RavenDB itself authenticates clients using x509 client certificates and does not implement Windows Authentication.
- You can run a separate HTTPS endpoint that performs Windows Authentication and then talks to RavenDB using a RavenDB client certificate.
- This document shows two supported approaches while reusing the same server certificate on a different port/hostname.

When to use this
- You want browser users to sign in with Windows Authentication (Kerberos/NTLM/Negotiate).
- You must keep RavenDB secured with mutual TLS and do not want to change RavenDB’s built‑in security model.

Important constraints
- The browser’s client certificate prompt happens during the TLS handshake, before HTTP headers exist. You cannot dynamically choose between a client certificate and Windows Auth on the same TLS connection/port.
- Run Windows Auth on a separate listener (port or hostname). That listener acts as a gateway/proxy/middle‑tier to RavenDB.
- RavenDB will authorize the identity embedded in the certificate presented by your gateway, not the end user. Enforce per‑user authorization in the gateway before it calls RavenDB.

Option A: IIS/HTTP.SYS site with Windows Authentication
1) Bind HTTPS on an alternate port using the same server certificate
   - Install your server certificate in the Local Machine\Personal store.
   - Bind an IIS site (or HTTP.SYS binding via netsh http add sslcert) to, for example, https://your-host:8443 using the same certificate thumbprint used by RavenDB. Using the same certificate on multiple bindings is supported.
   - Enable Windows Authentication on the site (disable Anonymous if you want every request to be authenticated).

2) Proxy to RavenDB using a RavenDB client certificate
   - Install a RavenDB client certificate (service identity) in the Local Machine store.
   - Grant the IIS Application Pool identity read access to the private key.
   - Configure Application Request Routing (ARR) + URL Rewrite to forward requests to RavenDB’s HTTPS endpoint (e.g., https://localhost:8080 or your cluster URL).
   - Configure ARR to present the RavenDB client certificate when connecting outbound to RavenDB.

3) Security and routing
   - Restrict which paths are proxied. Do not expose sensitive/admin endpoints unless necessary.
   - Enforce per‑user authorization at IIS or in your application before proxying.

Option B: Minimal ASP.NET Core gateway with Kestrel and Negotiate
Use a small ASP.NET Core app that listens on a separate HTTPS port, authenticates users with Windows (Negotiate), and forwards allowed operations to RavenDB using a RavenDB client certificate.

Example Program.cs (minimal sample)

// Program.cs
// Target framework: net8.0
// Packages: Microsoft.AspNetCore.Authentication.Negotiate

using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication.Negotiate;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to use your existing server certificate on a different port (e.g., 8443)
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8443, listenOptions =>
    {
        // Load the same server certificate RavenDB uses
        // Replace with your loading logic (store/PEM/PFX). Example uses thumbprint from config.
        var thumbprint = builder.Configuration["ServerCertificate:Thumbprint"];
        if (string.IsNullOrWhiteSpace(thumbprint))
            throw new InvalidOperationException("ServerCertificate:Thumbprint is required");

        var cert = LoadCertificateByThumbprint(thumbprint) ??
                   throw new InvalidOperationException($"Certificate with thumbprint {thumbprint} not found");

        listenOptions.UseHttps(cert);
    });
});

// Enable Windows Authentication (Negotiate)
builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();
builder.Services.AddAuthorization();

// Configure HttpClient that presents a RavenDB client certificate when calling RavenDB
builder.Services.AddHttpClient("Raven", httpClient =>
{
    httpClient.BaseAddress = new Uri(builder.Configuration["Raven:BaseUrl"]!); // e.g., https://localhost:8080
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var ravenCertThumb = builder.Configuration["Raven:ClientCertificateThumbprint"];
    if (string.IsNullOrWhiteSpace(ravenCertThumb))
        throw new InvalidOperationException("Raven:ClientCertificateThumbprint is required");

    var handler = new HttpClientHandler
    {
        ClientCertificateOptions = ClientCertificateOption.Manual,
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator // for lab only; validate in production
    };

    var clientCert = LoadCertificateByThumbprint(ravenCertThumb) ??
                     throw new InvalidOperationException($"Raven client certificate {ravenCertThumb} not found");
    handler.ClientCertificates.Add(clientCert);
    return handler;
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Simple example route that requires Windows identity and proxies to RavenDB
app.MapGet("/databases/{db}/docs/{id}", async (string db, string id, IHttpClientFactory factory, HttpContext ctx) =>
{
    // Enforce per-user authorization here (role checks, ACL, etc.)
    if (ctx.User?.Identity?.IsAuthenticated != true)
        return Results.Unauthorized();

    var client = factory.CreateClient("Raven");
    var upstream = $"/databases/{WebUtility.UrlEncode(db)}/docs?id={WebUtility.UrlEncode(id)}";
    using var resp = await client.GetAsync(upstream, ctx.RequestAborted);
    return Results.Stream(resp.Content.ReadAsStream(), contentType: resp.Content.Headers.ContentType?.ToString());
}).RequireAuthorization();

app.Run();

static X509Certificate2? LoadCertificateByThumbprint(string thumbprint)
{
    thumbprint = thumbprint.Replace(" ", string.Empty).ToUpperInvariant();
    using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
    store.Open(OpenFlags.ReadOnly);
    var certs = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false);
    return certs.Count > 0 ? certs[0] : null;
}

Suggested appsettings.json keys
{
  "ServerCertificate": {
    "Thumbprint": "THUMBPRINT_OF_SERVER_CERT"
  },
  "Raven": {
    "BaseUrl": "https://localhost:8080",
    "ClientCertificateThumbprint": "THUMBPRINT_OF_RAVENDB_CLIENT_CERT"
  }
}

Notes
- Reusing the same server certificate on multiple bindings (ports/hostnames) is acceptable. Ensure the certificate’s Subject/Subject Alternative Names cover the hostnames you present to clients.
- The sample’s DangerousAcceptAnyServerCertificateValidator is only for local testing. In production, validate RavenDB’s server certificate properly (e.g., via CA trust or pinning the certificate thumbprint).
- The gateway should expose only the minimal set of endpoints needed, and must enforce per‑user authorization before forwarding to RavenDB.

Troubleshooting
- 401 loops in browsers typically indicate missing SPNs/URL registrations for Kerberos or disabled Windows Authentication.
- 403 from RavenDB indicates the client certificate used by the gateway lacks permissions; grant the appropriate database/cluster permissions to that certificate within RavenDB.
