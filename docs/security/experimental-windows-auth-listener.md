Experimental: Optional second HTTPS listener with Windows Authentication (Negotiate) on Windows

Status
- This experimental feature lets RavenDB bind a second HTTPS listener (separate port/host) using the same server certificate and perform Windows Authentication (Negotiate) for requests arriving on that listener (Windows only).
- Scope: Authentication is performed via the OS (Kerberos/NTLM/Negotiate). Authorization inside RavenDB remains certificate‑based unless otherwise noted below.
- Purpose: Provide a built‑in way to authenticate browser users with Windows on a dedicated port while keeping the primary listener unchanged and certificate‑secured.

Configuration keys
- Http.WindowsAuth.Enabled = false (bool)
  - When true, RavenDB starts an additional HTTPS listener on Windows and enables the Negotiate authentication handler for requests reaching that listener.
- Http.WindowsAuth.Port = <port> (int, required when Enabled=true)
  - Must differ from the primary HTTPS port.
- Http.WindowsAuth.Host = <hostname or IP> (string, optional)
  - If omitted, RavenDB binds the same addresses as the primary listener.
- Http.WindowsAuth.AuthorizeAsClusterAdmin = false (bool)
  - If true, a successfully Windows‑authenticated user on the WindowsAuth listener is authorized as ClusterAdmin. This is a highly privileged mode intended for controlled environments only. Defaults to false.

Platform constraints
- Only supported on Windows. When enabled on non‑Windows platforms, RavenDB logs an info message and skips creating the listener.

Certificate and authentication behavior
- The secondary listener uses the same server certificate as the primary listener for TLS.
- On the WindowsAuth listener only, RavenDB enables the Negotiate authentication handler. When a request arrives on that port, RavenDB challenges with Windows Authentication and, on success, maps the Windows principal to RavenDB’s internal authentication feature for the lifetime of that connection.
  - If Http.WindowsAuth.AuthorizeAsClusterAdmin = true, the authenticated principal is granted ClusterAdmin.
  - If false (default), the principal is treated as an authenticated user with limited access; per‑database permissions still need to be enforced by certificates or future policy mapping (not yet implemented).

Logging and diagnostics
- If misconfigured (e.g., missing port, same port as primary) or when running on a non‑Windows OS, RavenDB logs an informational message and skips the extra listener.
- On success, RavenDB logs that the experimental WindowsAuth listener was started and that Negotiate has been registered.

Security considerations
- AuthorizeAsClusterAdmin is extremely powerful. Only enable it in trusted environments after careful review.
- Per‑user authorization in RavenDB is still certificate‑centric. The experimental WindowsAuth listener authenticates users but does not yet provide fine‑grained, user‑mapped authorization across databases and operations. Plan your exposure accordingly (e.g., restrict to admin tasks on secured networks).

Recommended approach today
- For most deployments, continue to use the supported patterns documented in:
  - docs/security/authentication-options.md
  - docs/security/windows-auth-separate-endpoint.md
  These cover placing Windows Authentication in front of RavenDB via IIS/HTTP.SYS or a separate Kestrel application and connecting to RavenDB using a client certificate.
- The experimental WindowsAuth listener is intended for evaluation and highly controlled scenarios, and its behavior might change in future versions.
