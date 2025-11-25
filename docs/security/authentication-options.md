Authentication in RavenDB: Certificates vs. Windows Authentication

Summary
- RavenDB’s built-in authentication mechanism is mutual TLS (client certificates).
- Native Windows Authentication (Kerberos/NTLM/Negotiate) is not supported by RavenDB at this time.
- You cannot combine “client certificates” and “Windows Authentication” on the same HTTPS endpoint and choose between them dynamically per request.
- If you need Windows Authentication for users in a browser, use a reverse proxy that performs Windows Auth in front of RavenDB, understanding the trade-offs explained below.

Why dynamic choice (per request) is not possible
- The browser’s certificate prompt is part of the TLS handshake, which happens before any HTTP headers or request body are exchanged.
- Because headers are only available after TLS is established, a server cannot inspect a header to decide whether it should ask the browser for a client certificate or challenge with Windows Auth.
- Likewise, you cannot defer the choice to “later” in the pipeline on the same connection. The handshake has already decided whether a client certificate is presented.

What is supported today
- RavenDB supports mutual TLS (client certificates) for both server-to-server and client-to-server authentication and authorization.
- Authorization is based on the identity embedded in the client certificate and the permissions assigned to that certificate inside RavenDB.

Recommended deployment patterns when Windows Authentication is required
1) Separate endpoints/hostnames
   - Keep RavenDB secured with certificates on its own hostname/port (e.g., raven-db.internal:443) and manage/administer it with client certificates as usual.
   - Provide a separate application (e.g., an internal web app or API) on another hostname (e.g., raven-portal.internal) that uses Windows Authentication for end users and talks to RavenDB from the server side using a service certificate.
   - Pros: Clean separation, preserves RavenDB’s security model; Cons: The server-side app must handle per-user authorization rules, since RavenDB sees only the service identity.

2) Reverse proxy with Windows Authentication in front of RavenDB
   - Place IIS/HTTP.SYS (Windows), Nginx, or another proxy in front of RavenDB.
   - Configure the proxy to terminate TLS and perform Windows Authentication for users.
   - Configure the proxy-to-RavenDB connection to use a RavenDB client certificate (a service identity) and to forward requests to RavenDB.
   - Important: RavenDB does not accept a forwarded user identity (e.g., X-Forwarded-User) as an authorization source. Authorization will be performed based on the proxy’s client certificate, not the end user. Your proxy or a custom middle-tier must enforce per-user authorization before forwarding.
   - Pros: End users authenticate with Windows via the proxy; Cons: RavenDB will not have per-user visibility unless a custom integration layer is implemented.

Why not both on one port/hostname
- TLS client certificate negotiation and Windows Authentication are mutually exclusive at the protocol phase that matters. Only one challenge can occur for a given TLS connection.
- Running both on the same port and deciding per request would require protocol features RavenDB and standard browsers do not support.

Typical IIS setup (reverse proxy)
1. Bind a site in IIS with HTTPS and enable Windows Authentication (disable Anonymous, or keep as needed for specific paths).
2. Install the RavenDB client certificate (service identity) into the Local Machine certificate store and grant the IIS App Pool user access to the private key.
3. Configure Application Request Routing (ARR) + URL Rewrite to proxy traffic to RavenDB’s HTTPS endpoint.
4. Configure the ARR outbound connection to present the RavenDB client certificate when calling RavenDB.
5. Restrict which RavenDB paths are exposed (e.g., only endpoints your users need). Enforce per-user authorization in IIS or your application before proxying to RavenDB, since RavenDB will authorize the proxy’s certificate, not the end user.

Security considerations
- Do not trust identity headers (e.g., X-Forwarded-User) inside RavenDB; RavenDB does not treat them as authentication.
- Limit and audit what the reverse proxy can do by assigning the minimum necessary permissions to the proxy’s RavenDB certificate.
- Prefer separate middle-tier services for complex authorization, rather than exposing RavenDB Studio through a proxy with Windows Authentication.

FAQ
Q: Can RavenDB be configured to use Windows Authentication directly?
A: Not at this time. RavenDB’s security model is built around x509 client certificates.

Q: Can I make the browser show either the certificate prompt or the Windows login prompt based on a header?
A: No. The certificate prompt occurs during TLS handshake before headers exist. You cannot select authentication method based on an HTTP header.

Q: Can I run both methods side by side?
A: Yes, by using separate endpoints/hostnames or by placing a reverse proxy that handles Windows Auth and connects to RavenDB via certificate. Note that RavenDB will authorize the proxy’s certificate identity, not the end user’s, unless you build an integration layer that maps users to RavenDB operations before forwarding.
