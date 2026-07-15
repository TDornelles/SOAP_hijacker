# LIVE CAPTURE — GLP Rate endpoint (2026-07-15)

Direct call to the production GLP rates endpoint, made from outside the adapter (curl) to capture
the real request/response contract before IIS setup. Request values are the adapter's exact
inbound mapping of `fixtures/rate-request.xml` (the real captured Summit request, account 3528,
US → NZ) — i.e. this is byte-for-byte what `GlpClient` would POST for that fixture.

## Request

```
POST https://glp.gdnparcel.com/api/v1/shipping/rates
Content-Type: application/json
(no auth — Rate is public; X-API-Key is Ship-only)
```

```json
{
  "accountNumber": "3528",
  "destinationCountryCode": "NZ",
  "weight": 0.45,
  "weightUOM": "Pounds",
  "length": 11,
  "width": 7,
  "height": 2,
  "dimensionUOM": "Inches",
  "packageValue": 12.99
}
```

## Response

`HTTP/2 200` — 2.18 s total, remote IP 3.128.20.43, served `Wed, 15 Jul 2026 18:10:01 GMT`.

```
cache-control: no-cache, no-store, max-age=0, must-revalidate
pragma: no-cache
content-type: application/json
expires: 0
x-content-type-options: nosniff
x-xss-protection: 0
x-frame-options: DENY
x-powered-by: ARR/3.0
x-powered-by: ASP.NET
content-length: 255
```

```json
[{"FreightCost":"22.92","FuelSurcharge":"5.73","TotalFreightCost":"28.65","DutyValue":"0.00","VatValue":"0.00","TotalTaxesDuties":"0.00","TotalCost":"28.65","CurrencyCode":"USD","BillableWeight":1.11,"BillableWeightUOM":"Pounds","DimensionalWeight":1.11}]
```

(body above is verbatim, unformatted — 255 bytes, single-element array)

## Observations

1. **Response is a JSON array** of rate objects (one element here). `LandedCostResult.FromJson`
   already handles the array shape (first element). Field names are **PascalCase** and match
   `LandedCostResult` property names exactly — no unknown or missing fields.
2. **Money figures are JSON strings** (`"FreightCost":"22.92"`), not numbers; only
   `BillableWeight`/`DimensionalWeight` are numeric. ⚠️ **This breaks the current parser**:
   `LandedCostResult`'s `decimal?` properties are deserialized without
   `JsonNumberHandling.AllowReadingFromString`, so `FromJson` throws `JsonException` on this real
   body (verified against this exact capture: `The JSON value could not be converted to
   System.Nullable[System.Decimal]. Path: $.FreightCost`). Until fixed, every ported Rate call
   would return a SOAP Fault ("GLP returned a non-JSON body") even though GLP answered 200.
3. `x-powered-by: ARR/3.0` — `glp.gdnparcel.com` is fronted by IIS ARR (consistent with GLP living
   behind IIS on the shared box). Reinforces the CLAUDE.md note that `Glp:BaseUrl` could point at
   GLP's local port directly to skip the hairpin.
4. Billable weight (1.11 lb) exceeds actual weight (0.45 lb) — dimensional weight pricing
   (11×7×2 in), sanity-checking that GLP actually rated this package.
5. This capture confirms the **GLP JSON side** only. The SOAP `<RateLandedCostResult>` element
   names in `OutboundFieldMap` (STUBBED MAPPING #2) still need a captured **legacy Summit SOAP
   response** to confirm — this file does not resolve that TODO.

Reproduce:

```sh
curl -sS -X POST 'https://glp.gdnparcel.com/api/v1/shipping/rates' \
  -H 'Content-Type: application/json' --data @body.json
```
