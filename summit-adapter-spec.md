# Build Spec — Summit SOAP→JSON Adapter

A spec for Claude Code to scaffold and implement this project. This document defines
**what** to build and the contracts to honor. It does not prescribe implementation code.

---

## 1. Goal

Summit (GDN's largest client) sends requests to a legacy WCF SOAP service and refuses to
change anything on their side. Build a standalone adapter that sits at the same URL Summit
already calls, translates each SOAP request into JSON for our existing Spring Boot service
("GLP"), forwards it, then translates the JSON response back into the SOAP shape Summit
expects. Summit must see no difference.

The adapter is a **pure translator + router**. It never touches the database itself — all
data work happens in GLP behind it.

---

## 2. Tech stack (decided — do not deviate)

- .NET 8, ASP.NET Core Web API.
- Hosted in IIS on Windows Server (in-process hosting).
- No WCF/CoreWCF server stack. SOAP is handled by reading the raw XML body and building
  raw XML responses (Summit reads returned values loosely; there is no WSDL to honor).
- HTTP client (typed) for calling GLP.
- xUnit for tests.

---

## 3. The legacy contract (confirmed facts)

- **Single endpoint** for every operation:
  `/Routing/Service/Soap/V2.6/Routing.svc`
  served over both http and https at `pds.gdnparcel.com`.
- **SOAP 1.1.** Request `Content-Type: text/xml; charset=utf-8`; responses must use the
  same content type.
- The **operation is selected by the `SOAPAction` HTTP header**, e.g.
  `http://tempuri.org/IRouting/RateLandedCost`. It is NOT selected by URL path.
- Request envelope shape:
  - A `soap:Header` containing an empty `wsse:Security` element with `mustUnderstand="1"`.
    The adapter must **ignore** this header (do not enforce mustUnderstand).
  - A `soap:Body` whose single child is the operation element:
    `<OperationName xmlns="http://tempuri.org/"><rData>…</rData></OperationName>`.
- Known operations in the service: `Rate`, `RateLandedCost`, `RouteDelivery`,
  `RouteDeliveryRate`, `RouteDeliveryRateLandedCost`, `RouteUpdateDelivery`,
  `RouteVoidDelivery`, `GetTrackingHistory`, `CloseEODProcess`.

### Operation families → behavior

- **Rate family** (`Rate`, `RateLandedCost`): read-only price check → call GLP's rate
  endpoint → **no database write**.
- **Route family** (`RouteDelivery`, `RouteDeliveryRateLandedCost`, …): creates the
  delivery → call GLP's ship endpoint → **writes `tbl_consignee`** (GLP owns the write).

The first deliverable only needs **one rate op and one route op** wired end-to-end. The
rest should exist as registry entries that are easy to fill in later.

---

## 4. Two mappings that are NOT yet confirmed — STUB, do not invent

These two are the only parts that depend on data we haven't captured from Summit yet.
Implement them behind a single, clearly-marked seam each, with a `// TODO(confirm): …`
comment and a sensible default. **Do not fabricate plausible-looking field names and bury
them in logic** — keep them in one obvious place so they can be corrected from one real
captured request/response.

1. **Inbound: element names inside `<rData>`.** Every sample request had an empty
   `<rData/>`, so the real child element names are unknown. Default to matching the JSON
   DTO field names (section 6), looked up namespace-agnostically.
2. **Outbound: element names inside `<…Result>`.** Sample responses were empty. Default
   to PascalCase tags matching the landed-cost fields (section 6).

---

## 5. Behavior spec (the request lifecycle)

For a POST to the `.svc` path:

1. Read the raw request body as UTF-8 text.
2. Determine the operation name: from `SOAPAction` (strip quotes, take the segment after
   the last `/`); fall back to the local name of the first child of `soap:Body`.
3. Look the operation up in the registry. Unknown operation → SOAP Fault (`Client`).
4. Parse `<rData>` into the package DTO (stubbed mapping #1). Parse failure → SOAP Fault
   (`Client`) with a readable message.
5. Forward the DTO as JSON to the GLP endpoint named by the registry entry (rate vs ship).
6. Non-success from GLP → SOAP Fault (`Server`); log status + body.
7. On success, build the SOAP response envelope for that operation (stubbed mapping #2),
   filling the landed-cost fields from GLP's JSON. Return with `text/xml; charset=utf-8`.

Errors are always returned as **SOAP 1.1 Faults**, never bare HTTP error pages — Summit's
client expects an XML envelope back regardless.

---

## 6. Data contracts

### Inbound DTO sent to GLP (JSON, camelCase)
Fields and constraints:

- `accountNumber` — string, required, max 4 chars
- `destinationCountryCode` — string, required, exactly 2 chars (ISO)
- `weight` — number, required, > 0
- `weightUOM` — string, optional, `Pounds` | `Kilograms` (default Pounds)
- `length`, `width`, `height` — numbers, required, > 0
- `dimensionUOM` — string, optional, `Inches` | `Centimeters` (default Inches)
- `packageValue` — number, required, > 0

The adapter forwards values; GLP performs authoritative validation. The adapter only needs
enough validation to fail fast with a clean Fault on obviously malformed XML/numbers.

### GLP rate/ship response (JSON) → mapped into the SOAP Result
GLP returns these figures (a single object, or an array whose first element is used):

`FreightCost`, `FuelSurcharge`, `TotalFreightCost`, `DutyValue`, `VatValue`,
`TotalTaxesDuties`, `TotalCost`, `CurrencyCode`, `BillableWeight`, `BillableWeightUOM`,
`DimensionalWeight`.

Each becomes an element inside `<{Operation}Result>` in the response envelope (tag names
per stubbed mapping #2).

---

## 7. Configuration (appsettings.json)

- `Glp:BaseUrl` — base URL of the Spring Boot service (e.g. `http://localhost:8080`).
- `Glp:RatePath` — rate endpoint path (no DB write).
- `Glp:ShipPath` — ship endpoint path (writes `tbl_consignee`).
- Standard logging config. No secrets in source; environment-overridable.

Nothing about operation routing should be hardcoded in handlers — it lives in the registry,
and endpoint paths live in config.

---

## 8. Suggested project structure

- `SummitAdapter/` (Web API project)
  - `Program.cs` — host, DI, IIS in-process, map the single endpoint.
  - `Endpoints/` — the `.svc` request handler.
  - `Dispatch/` — operation registry (SOAPAction → {writesDb, glp path key, response namespace}).
  - `Soap/` — request parser, response builder, fault builder (the two stubbed seams live here).
  - `Models/` — package request DTO, landed-cost result.
  - `Services/` — typed GLP client.
  - `appsettings.json`.
- `SummitAdapter.Tests/` (xUnit) — see section 9.
- `README.md` — how to run locally, how to point at GLP, the two TODO mappings, and the
  IIS deployment steps from section 10.

---

## 9. Testing requirements

Build tests that run **without Summit and without a live GLP** (mock the GLP client):

- Operation resolution: SOAPAction header parsed correctly; fallback to body element when
  the header is absent; unknown operation → Fault.
- Rate family routes to the rate path and never the ship path; route family routes to ship.
- `rData` parser: produces the correct DTO from a populated request fixture; missing
  required fields / bad numbers → Fault.
- Response builder: GLP JSON (object and array forms) → well-formed SOAP envelope with the
  expected Result elements; XML special characters escaped.
- Fault builder: malformed XML in, valid SOAP 1.1 Fault out.
- End-to-end (in-memory host + mocked GLP): SOAP in → SOAP out for one rate op and one
  route op.

Include sample request/response XML fixtures under a `fixtures/` folder, clearly marked as
placeholders until real Summit captures replace them.

---

## 10. IIS deployment notes (document in README; do not script destructively)

- Publish framework-dependent or self-contained; host **in-process** under a dedicated app
  pool (No Managed Code).
- The site must answer at the exact path `/Routing/Service/Soap/V2.6/Routing.svc` on
  `pds.gdnparcel.com`, over both http and https, so Summit's existing calls land on it
  unchanged.
- Confirm the `web.config` `aspNetCore` handler is present after publish.
- Cutover is a hosting/routing change (pointing that path at this app) — call it out in the
  README but do not attempt to modify server DNS/IIS bindings from code.

---

## 11. Guardrails for this build

- Do **not** add a WCF/CoreWCF server, generate a WSDL, or add SOAP middleware packages.
- Do **not** write to any database from the adapter.
- Do **not** invent the two unconfirmed mappings into hard-to-find places — keep each in
  one file behind a `TODO(confirm)` marker with a default.
- Do **not** strip or require the `wsse:Security` header — ignore it.
- Keep the adapter stateless; all business logic and persistence stay in GLP.
- Every error path returns a SOAP 1.1 Fault with `text/xml; charset=utf-8`.

---

## 12. Build order (phased)

1. Scaffold project + test project + appsettings + README skeleton.
2. Models, registry, and the typed GLP client (with the two operations wired in config).
3. Request handler + SOAP parser + response builder + fault builder (mappings as marked
   stubs).
4. Tests for every item in section 9, with placeholder fixtures.
5. README: local run, GLP wiring, the two TODO mappings, IIS deployment steps.

Stop after each phase builds and its tests pass before moving on.
