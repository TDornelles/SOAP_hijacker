# Summit SOAP→JSON Adapter

A standalone .NET 8 adapter that sits at the exact URL Summit already calls. It is a **selective
hijacker**: for the operations that have been ported it translates the legacy WCF **SOAP 1.1**
request into JSON for our existing Spring Boot service (**GLP**), forwards it, and translates GLP's
JSON response back into the SOAP shape Summit expects. Every operation it has **not** ported it
forwards to the legacy service unchanged and relays the response verbatim. Either way Summit sees no
difference.

The adapter is stateless and **never touches the database** — all data work (including the
`tbl_consignee` write on ported Route operations) happens in GLP behind it. It ports operations
**one at a time**; the end goal is that every operation is translated.

> Full requirements: [`summit-adapter-spec.md`](./summit-adapter-spec.md).

---

## How it works

```
                                    ┌──JSON──▶  GLP (Spring Boot)   ← ported ops (translate)
Summit ──SOAP/XML──▶  Adapter ──────┤
        ◀─SOAP/XML──  (this app)    └──SOAP──▶  Legacy Routing.svc  ← everything else (pass-through)
```

- **One endpoint** for every operation: `POST /Routing/Service/Soap/V2.6/Routing.svc`.
- The operation is chosen by the **`SOAPAction` HTTP header** (e.g.
  `http://tempuri.org/IRouting/RateLandedCost`), not the URL path. If the header is missing, the
  adapter falls back to the local name of the first child of `soap:Body`. **Because every operation
  shares one URL, the translate/pass-through split can only happen here, at L7 — never by DNS or
  path.**
- The empty `wsse:Security` header (`mustUnderstand="1"`) is **ignored** — never enforced.
- Ported operations that error return a **SOAP 1.1 Fault** with `text/xml; charset=utf-8`, never a
  bare HTTP error page. Pass-through operations return whatever the legacy service returns.

### Operation disposition

Each operation is listed on its own line in the registry with its own disposition, so operations are
ported independently. Current state:

| Operation(s) | Disposition | GLP endpoint | DB write |
| --- | --- | --- | --- |
| `Rate`, `RateLandedCost` | **Translate** (ported) | `Glp:RatePath` | none |
| `RouteDelivery`, `RouteDeliveryRate`, `RouteDeliveryRateLandedCost` | Pass-through — *port at go-live* | (`Glp:ShipPath` when ported) | GLP writes `tbl_consignee` when ported |
| `RouteUpdateDelivery`, `RouteVoidDelivery`, `GetTrackingHistory`, `CloseEODProcess` | Pass-through | — | stays on legacy |
| any un-enumerated `SOAPAction` | Pass-through (default) | — | stays on legacy |

Routing lives entirely in the registry (`SummitAdapter/Dispatch/OperationRegistry.cs`) and endpoint
paths live in config — nothing is hardcoded in handlers. **To port an operation, flip its one line
from `PassThrough` to `Translate(…, GlpEndpoint.Ship, writesDb: true)`.** Only the read-only Rate
operations are ported today; the new-package-insertion Route operations go live at cutover.

---

## Project layout

```
SummitAdapter/
  Program.cs            host, DI, IIS in-process, the single endpoint mapping
  Endpoints/            SoapHandler — the .svc request lifecycle
  Dispatch/             OperationRegistry, OperationDescriptor, OperationRouting, GlpEndpoint
  Soap/                 OperationResolver, request parser, response builder, fault builder
                        InboundFieldMap / OutboundFieldMap  ◀── the two stubbed seams
  Models/               PackageRequest (to GLP), LandedCostResult (from GLP)
  Services/             IGlpClient + GlpClient           (typed HttpClient → GLP, ported ops)
                        ILegacyForwarder + LegacyForwarder (typed HttpClient → legacy, pass-through)
  Options/              GlpOptions, LegacyOptions
  appsettings.json
SummitAdapter.Tests/    xUnit tests (run without Summit and without a live GLP)
fixtures/               PLACEHOLDER request/response XML (see fixtures/README.md)
```

---

## Run locally

Requires the **.NET 8 SDK**.

```bash
dotnet restore
dotnet build
dotnet test                                  # all tests, no Summit / no live GLP needed
dotnet run --project SummitAdapter           # serves on the URLs in launchSettings.json
```

Send a sample request (uses the placeholder fixture):

```bash
curl -i \
  -H 'Content-Type: text/xml; charset=utf-8' \
  -H 'SOAPAction: http://tempuri.org/IRouting/RateLandedCost' \
  --data-binary @fixtures/rate-request.xml \
  http://localhost:5080/Routing/Service/Soap/V2.6/Routing.svc
```

(With no GLP running you'll get a `soap:Server` Fault — expected; point it at GLP below.)

### Point it at GLP and the legacy origin

Configuration lives in the `Glp` and `Legacy` sections of `appsettings.json` and is
environment-overridable. No secrets in source.

| Key | Meaning | Default |
| --- | --- | --- |
| `Glp:BaseUrl` | Base URL of the Spring Boot service | `http://localhost:8080` |
| `Glp:RatePath` | Rate endpoint path (no DB write) | `/api/rate` |
| `Glp:ShipPath` | Ship endpoint path (writes `tbl_consignee`) | `/api/ship` |
| `Legacy:BaseUrl` | Base URL of the **relocated** legacy `Routing.svc` origin for pass-through ops | `http://localhost:9090` |

`Legacy:BaseUrl` must **not** resolve back to the adapter's own host, or pass-through traffic loops.

Override per environment without editing source, e.g.:

```bash
export Glp__BaseUrl="http://glp.internal:8080"
export Glp__RatePath="/v1/rate"
export Glp__ShipPath="/v1/ship"
export Legacy__BaseUrl="http://pds-origin.internal"
```

---

## ⚠️ The two unconfirmed mappings (STUB — do not invent)

Two things depend on data we have not yet captured from Summit (every sample request/response was
empty). Each lives behind **one obvious seam** with a `TODO(confirm)` marker and a sensible default.
**Do not** scatter guessed field names through the logic — change only these two files when a real
capture arrives.

1. **Inbound — child element names inside `<rData>`**
   → `SummitAdapter/Soap/InboundFieldMap.cs`
   Default: the camelCase JSON DTO field names (section 6), looked up namespace-agnostically.

2. **Outbound — element names inside `<…Result>`**
   → `SummitAdapter/Soap/OutboundFieldMap.cs`
   Default: PascalCase tags matching the landed-cost fields (section 6).

When you capture one real request + response from Summit: update those two files and replace the
`fixtures/*.xml` placeholders. Nothing else should need to change.

---

## Data contracts (section 6)

**Inbound DTO → GLP (JSON, camelCase).** The adapter forwards values; GLP does authoritative
validation. The adapter only validates enough to fail fast with a clean Fault on malformed input.

| Field | Type | Constraint |
| --- | --- | --- |
| `accountNumber` | string | required, max 4 |
| `destinationCountryCode` | string | required, exactly 2 (ISO) |
| `weight` | number | required, > 0 |
| `weightUOM` | string | `Pounds` \| `Kilograms` (default Pounds) |
| `length`, `width`, `height` | number | required, > 0 |
| `dimensionUOM` | string | `Inches` \| `Centimeters` (default Inches) |
| `packageValue` | number | required, > 0 |

**GLP response → SOAP Result.** GLP returns a single object, or an array whose first element is
used: `FreightCost`, `FuelSurcharge`, `TotalFreightCost`, `DutyValue`, `VatValue`,
`TotalTaxesDuties`, `TotalCost`, `CurrencyCode`, `BillableWeight`, `BillableWeightUOM`,
`DimensionalWeight`. Each becomes an element inside `<{Operation}Result>` (tag names per mapping #2).

---

## Tests

`dotnet test` runs everything in-process with GLP mocked (`FakeGlpClient`) — no Summit, no live GLP:

- **Operation resolution** — `SOAPAction` parsed correctly; body-element fallback.
- **Routing** — ported Rate ops translate to the rate path and never ship; un-ported ops (Route,
  tracking, EOD, and any un-enumerated `SOAPAction`) pass through to the legacy forwarder, not GLP.
- **`rData` parser** — correct DTO from a populated request; missing/invalid fields → Fault.
- **Response builder** — object and array GLP forms → well-formed envelope; XML special chars escaped.
- **Fault builder** — malformed XML in → valid SOAP 1.1 Fault out; a legacy outage → Server Fault.
- **End-to-end** — in-memory host with GLP and the legacy forwarder mocked: a Rate op translates to
  SOAP out; a Route op is relayed to the legacy service verbatim.

Fixtures under `fixtures/` are **placeholders** until real Summit captures replace them.

---

## IIS deployment (section 10)

The adapter writes nothing and never modifies DNS/IIS bindings from code. Cutover is a
**hosting/routing change** done by hand. Below is the full runbook; in-process hosting is
preconfigured via the checked-in [`SummitAdapter/web.config`](./SummitAdapter/web.config) and
`<AspNetCoreHostingModel>InProcess</AspNetCoreHostingModel>` in the csproj.

### 1. Server prerequisites (order matters)

1. Install the **IIS** role (Server Manager → Web Server (IIS)).
2. Install the **.NET 8 Hosting Bundle** (`dotnet-hosting-8.0.x-win.exe`) — it installs the ASP.NET
   Core Module v2 **and** the runtime. Install it **after** IIS, then refresh IIS:
   ```powershell
   net stop was /y; net start w3svc    # or: iisreset
   ```
3. Confirm: `dotnet --list-runtimes` shows `Microsoft.AspNetCore.App 8.0.x`.

### 2. Publish (framework-dependent, Windows apphost)

From the build box (the `-r win-x64 --self-contained false` produces the Windows `.exe` apphost
while staying framework-dependent — needed when building on Linux/macOS):

```bash
dotnet publish SummitAdapter -c Release -r win-x64 --self-contained false -o ./publish
```

Copy `./publish` to the server, e.g. `C:\inetpub\SummitAdapter`. Confirm the published `web.config`
contains the `aspNetCore` handler entry.

### 3. App pool

Create app pool **SummitAdapter**: .NET CLR version = **No Managed Code**, Integrated pipeline.
(In-process is selected by `web.config` `hostingModel="inprocess"`.) Grant the app pool identity
**read** access to the publish folder.

### 4. Site — ⚠️ deploy at the SITE ROOT

The route is the absolute path `/Routing/Service/Soap/V2.6/Routing.svc`, so the app must serve at
the **root** of the host. **Do not** create an IIS *application* at a `/Routing` virtual path — that
prepends `/Routing` and breaks the URL. Create a dedicated **site**: physical path = publish folder,
app pool = SummitAdapter, host name `pds.gdnparcel.com`.

### 5. Bindings (http + https — Summit uses both)

- **http**: host `pds.gdnparcel.com`, port 80.
- **https**: host `pds.gdnparcel.com`, port 443, bound to the TLS cert for that host (import to
  `LocalMachine\My`; enable SNI if the box hosts other certs).

### 6. Configuration

Point `Glp:BaseUrl` at the GLP service and `Legacy:BaseUrl` at the **relocated** legacy origin (step
8), both reachable **from the server** — via `appsettings.Production.json` or the `Glp__BaseUrl` /
`Legacy__BaseUrl` env vars in `web.config`. Confirm `Legacy:BaseUrl` does **not** resolve back to
`pds.gdnparcel.com`. Ensure `ASPNETCORE_ENVIRONMENT=Production` (already set in the checked-in
`web.config`). From the server, confirm outbound reachability to **both** GLP and the legacy origin
(firewall).

### 7. Verify (smoke)

- Liveness: `Invoke-WebRequest http://pds.gdnparcel.com/health` → `200 OK`.
- Operation round-trip:
  ```powershell
  Invoke-WebRequest -Uri https://pds.gdnparcel.com/Routing/Service/Soap/V2.6/Routing.svc `
    -Method Post -ContentType 'text/xml; charset=utf-8' `
    -Headers @{ SOAPAction = 'http://tempuri.org/IRouting/RateLandedCost' } `
    -InFile .\fixtures\rate-request.xml
  ```
  Expect a SOAP envelope with `text/xml`. GLP reachable → a `RateLandedCostResult`; GLP down → a
  `soap:Server` Fault (still proves routing + content type).
- **Pass-through:** send an un-ported op (e.g. `SOAPAction: …/GetTrackingHistory`) and confirm the
  response matches the **legacy origin's**, not a Fault — proving the adapter forwards what it hasn't
  ported.
- ANCM errors (500.30 / 500.31 / 502.5): set `stdoutLogEnabled="true"` in `web.config`, recycle the
  app pool, reproduce, read `.\logs\stdout_*.log`, then set it back to `false`.

### 8. Cutover (coordinate; not automated)

This is a **selective** hijack, not a full reroute. The adapter takes over only the operations it has
ported (today the Rate ops; new package insertions at go-live) and forwards everything else — and the
rest of `pds.gdnparcel.com` — to the legacy service untouched. Because all operations share one
`.svc` URL and are selected by `SOAPAction`, the split happens **at the adapter (L7)**, not by DNS or
path. Steps:

1. **Relocate the legacy service first** to a new origin the adapter can reach (e.g.
   `pds-origin.internal`); set `Legacy:BaseUrl` to it. It must not resolve back to the adapter.
2. **Route only the `.svc` endpoint** on `pds.gdnparcel.com` to this server. Leave every other
   path/site on the host where it is — the adapter must not front the whole site.
3. Verify with step 7: a Rate op returns a translated result; an un-ported op returns the legacy
   origin's response verbatim.

Porting further operations later (e.g. package insertions) is a **code change** — flip the
operation's line in `OperationRegistry` from `PassThrough` to `Translate` and redeploy — not another
routing change.

---

## Guardrails honored

- No WCF/CoreWCF server, no generated WSDL, no SOAP middleware — raw XML in, raw XML out.
- No database writes from the adapter; all persistence stays in GLP.
- The two unconfirmed mappings are isolated behind single `TODO(confirm)` seams with defaults.
- The `wsse:Security` header is ignored, never required or stripped.
- Stateless adapter; every error path returns a SOAP 1.1 Fault with `text/xml; charset=utf-8`.
