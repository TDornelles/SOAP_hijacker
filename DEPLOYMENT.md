# Deployment Steps — Summit SOAP Adapter on Windows Server / IIS

A short checklist for getting the adapter live on the Windows Server. Full detail and
troubleshooting are in [`README.md`](./README.md#iis-deployment-section-10).

> The adapter writes nothing and never changes DNS/IIS bindings from code. Cutover is a manual
> hosting/routing change.

> **Scope — this is a selective hijacker, not a full reroute.** The adapter only takes over the
> operations it has ported to GLP; today that is the **Rate** operations (`Rate`, `RateLandedCost`),
> and **new package insertions** (`RouteDelivery…`) at go-live. Every other operation — and the rest
> of `pds.gdnparcel.com` — must keep reaching the **legacy service unchanged**. Two consequences:
> 1. Do **not** repoint the whole host at the adapter expecting only some calls to change. All
>    operations hit the **same** `.svc` URL and are selected by the `SOAPAction` header, so the
>    split happens at L7 (by `SOAPAction`), never by DNS or URL path.
> 2. The real `Routing.svc` must be **relocated to a new origin** the adapter can reach, and
>    `Legacy:BaseUrl` set to it. The adapter forwards every operation it hasn't ported to that
>    origin verbatim. The origin must **not** resolve back to the adapter, or pass-through loops.

---

## 1. Server prerequisites (order matters)
- [ ] Install the **IIS** role (Server Manager → Web Server (IIS)).
- [ ] Install the **.NET 8 Hosting Bundle** (`dotnet-hosting-8.0.x-win.exe`) — **after** IIS. It
      installs the ASP.NET Core Module v2 **and** the runtime.
- [ ] Refresh IIS: `net stop was /y; net start w3svc` (or `iisreset`).
- [ ] Verify: `dotnet --list-runtimes` shows `Microsoft.AspNetCore.App 8.0.x`.

## 2. Publish
- [ ] From the build box:
      `dotnet publish SummitAdapter -c Release -r win-x64 --self-contained false -o ./publish`
- [ ] Copy `./publish` to the server, e.g. `C:\inetpub\SummitAdapter`.
- [ ] Confirm the published `web.config` contains the `aspNetCore` handler entry.

## 3. App pool
- [ ] Create app pool **SummitAdapter**: .NET CLR version = **No Managed Code**, Integrated pipeline.
- [ ] Grant the app pool identity **read** access to the publish folder.

## 4. Site — ⚠️ deploy at the SITE ROOT
- [ ] Create a dedicated **site**: physical path = publish folder, app pool = SummitAdapter,
      host name `pds.gdnparcel.com`.
- [ ] Do **not** create an IIS *application* at a `/Routing` virtual path — it prepends `/Routing`
      and breaks the URL `/Routing/Service/Soap/V2.6/Routing.svc`.

## 5. Bindings (Summit uses both)
- [ ] **http** — host `pds.gdnparcel.com`, port 80.
- [ ] **https** — host `pds.gdnparcel.com`, port 443, bound to the TLS cert for that host
      (import to `LocalMachine\My`; enable SNI if needed).

## 6. Configuration
- [ ] Set `Glp:BaseUrl` to the GLP service reachable **from the server** — via
      `appsettings.Production.json` or the `Glp__BaseUrl` env var in `web.config`.
- [ ] Set `Legacy:BaseUrl` to the **relocated** legacy `Routing.svc` origin (see step 8), via
      `appsettings.Production.json` or the `Legacy__BaseUrl` env var. Confirm it does **not** resolve
      back to `pds.gdnparcel.com` (would loop pass-through traffic).
- [ ] Ensure `ASPNETCORE_ENVIRONMENT=Production` (already set in `web.config`).
- [ ] Confirm outbound reachability to **both** GLP and the legacy origin (firewall).

## 7. Verify (smoke)
- [ ] Liveness: `Invoke-WebRequest http://pds.gdnparcel.com/health` → `200 OK`.
- [ ] Operation round-trip:
      ```powershell
      Invoke-WebRequest -Uri https://pds.gdnparcel.com/Routing/Service/Soap/V2.6/Routing.svc `
        -Method Post -ContentType 'text/xml; charset=utf-8' `
        -Headers @{ SOAPAction = 'http://tempuri.org/IRouting/RateLandedCost' } `
        -InFile .\fixtures\rate-request.xml
      ```
      Expect a SOAP envelope with `text/xml`. GLP reachable → `RateLandedCostResult`; GLP down →
      `soap:Server` Fault (still proves routing).
- [ ] **Pass-through check** — send an un-ported op (e.g. `SOAPAction: …/GetTrackingHistory`) and
      confirm the response matches what the **legacy origin** returns, not a Fault. This proves the
      adapter is forwarding, not hijacking, everything it hasn't ported.
- [ ] On ANCM errors (500.30 / 500.31 / 502.5): set `stdoutLogEnabled="true"` in `web.config`,
      recycle the pool, reproduce, read `.\logs\stdout_*.log`, then set it back to `false`.

## 8. Cutover (manual, coordinated)
The adapter sits **in front of** the `.svc` endpoint and forwards everything it hasn't ported to the
legacy origin — so cutover is about inserting it into the path *without* losing the origin, not about
rerouting the whole site.
- [ ] **Relocate the legacy service first.** Move the existing `Routing.svc` to a new address the
      adapter can reach (e.g. `pds-origin.internal`) and confirm it answers there. Point
      `Legacy:BaseUrl` at it (step 6).
- [ ] **Insert the adapter at the endpoint only.** Route `pds.gdnparcel.com`'s
      `/Routing/Service/Soap/V2.6/Routing.svc` to this server. Leave every other path/site on
      `pds.gdnparcel.com` pointed where it is today — the adapter must not front them.
- [ ] Verify with step 7: a Rate op returns a translated result; an un-ported op returns the legacy
      origin's response verbatim.
- [ ] **Porting more ops later is a code change, not a routing change.** To bring a new package
      insertion live, flip its line in `OperationRegistry` from `PassThrough` to
      `Translate(…, GlpEndpoint.Ship, writesDb: true)`, publish, and redeploy — routing stays put.
