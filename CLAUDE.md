# Notes for Claude

## Deployment

- **Everything shares one Windows/IIS server.** The legacy PDS site (IIS site `PDS`, id 8) owns the
  `pds.gdnparcel.com` bindings and serves the whole PDS web app — not just `Routing.svc` — so the
  adapter must NOT take those bindings. The L7 split is done with **URL Rewrite + ARR** inside the
  `PDS` site: a rule proxies only `/Routing/Service/Soap/V2.6/Routing.svc` to the adapter site on a
  local port; the adapter's `Legacy:BaseUrl` points back at the PDS site via a dedicated extra
  binding (`127.0.0.1:8090`), and the rewrite rule is conditioned to skip that port so
  pass-through traffic doesn't loop. TLS stays terminated at the PDS site; the adapter listens on
  plain local HTTP. Rollback = disable the rewrite rule.
- **`Legacy:BaseUrl` must use the literal IPv4 `http://127.0.0.1:8090`, not `localhost`.** The PDS
  8090 binding is IPv4-only; `localhost` resolves to `::1` (IPv6) first on Windows, so the adapter's
  HttpClient misses the binding and hits another site's wildcard `:8090` listener, which answers
  HTTP 400 "Invalid Hostname". (Verified on the server 2026-07-19.)
- **As deployed the adapter site is `SOAP_hijacker`, physical path `C:\inetpub\SOAP_hijacker\app`,
  app pool `SOAP_hijacker` (No Managed Code), bound `127.0.0.1:8082`** (8081 was already taken).
  Smoke it on its own port: `http://localhost:8082/health`. PDS-DEV binds `localhost:80`, so
  `http://localhost/health` hits PDS-DEV, not the adapter.

- **GLP runs on the same Windows Server as the adapter** (under `C:\SpringApps\pdsv2`).
  `Glp:BaseUrl` in `appsettings.Production.json` is set to `https://glp.gdnparcel.com`; if
  GLP connectivity problems arise (DNS, firewall, TLS, hairpin routing back through the public
  hostname), consider switching it to a localhost URL with GLP's actual port, e.g.
  `http://localhost:8080` — traffic never needs to leave the box.
