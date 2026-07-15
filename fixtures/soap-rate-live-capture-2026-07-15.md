# LIVE CAPTURE — legacy `Routing.svc` RateLandedCost (2026-07-15)

Baseline call to the **live legacy SOAP endpoint** (pre-hijack: no rewrite rule exists yet, so this
is pure legacy behavior — the "before" picture for cutover verification). Request body is
`fixtures/rate-request.xml` (real Summit capture, account 3528, US → NZ) with one modification
noted below. Sent via curl from outside the network.

## What it took to get a 200 — three endpoint findings

1. **POST over HTTPS fails (empty 404).** `https://pds.gdnparcel.com/Routing/Service/Soap/V2.6/Routing.svc`
   serves its `?wsdl` fine over https, but a SOAP POST to it returns 404 with an empty body. The
   WSDL's `soap:address` declares `http://` — the WCF service endpoint appears to be bound to
   plain http only. **POSTs must use `http://`** (as the original Summit capture did). Relevant to
   the ARR rewrite rule: Summit traffic evidently arrives over http.
2. **The dev-capture WS-Addressing header is rejected (empty 400).** `rate-request.xml` carries
   `<Action s:mustUnderstand="1" xmlns="…/addressing/none">` (transcribed from the *dev* endpoint
   capture). The V2.6 endpoint 400s on it; stripping the entire `<s:Header>` block (SOAPAction
   HTTP header only) yields 200. The V2.6 binding evidently doesn't accept that addressing flavor.
3. **The dummy WSKEY is rejected at business level**, not transport: 200 OK with a
   `MessageEntity` error `10001 Invalid Account Details`. Re-run with a valid WSKEY (redacted
   here, same policy as rate-request.xml) produced the success response below.

## Request

```
POST http://pds.gdnparcel.com/Routing/Service/Soap/V2.6/Routing.svc
Content-Type: text/xml; charset=utf-8
SOAPAction: "http://tempuri.org/IRouting/RateLandedCost"
```

Body = `fixtures/rate-request.xml` **minus the `<s:Header>` block** (finding 2), i.e. envelope →
body → `<RateLandedCost>` → `<rData>` with WSKEY `00000000-…-000000000000` (dummy), account 3528,
NZ/4122, 0.45 lb, 11×7×2 in, value 12.99 USD.

## Response

`HTTP/1.1 200 OK` — 0.32 s, remote 3.128.20.43, `Content-Type: text/xml; charset=utf-8`,
`Content-Length: 772`, `X-AspNet-Version: 4.0.30319` (legacy .NET Framework WCF).

```xml
<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/"><s:Body><RateLandedCostResponse xmlns="http://tempuri.org/"><RateLandedCostResult xmlns:a="http://schemas.datacontract.org/2004/07/PDSRouting" xmlns:i="http://www.w3.org/2001/XMLSchema-instance"><a:AccountNumber>3528</a:AccountNumber><a:ResponseDateTime>2026-07-15T18:14:59.6365558+00:00</a:ResponseDateTime><a:RateResponseEntries><a:RateResponseEntry><a:BoxID>0</a:BoxID><a:RateEntity i:nil="true"/><a:LandedCostDetailEntities i:nil="true"/><a:MessageEntities><a:MessageEntity><a:Code>10001</a:Code><a:Description>Invalid Account Details</a:Description></a:MessageEntity></a:MessageEntities></a:RateResponseEntry></a:RateResponseEntries></RateLandedCostResult></RateLandedCostResponse></s:Body></s:Envelope>
```

## Success response (valid WSKEY, captured 2026-07-15 18:20 UTC)

`HTTP/1.1 200 OK` — 0.53 s, `Content-Length: 1494`. Same request, valid WSKEY substituted:

```xml
<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/"><s:Body><RateLandedCostResponse xmlns="http://tempuri.org/"><RateLandedCostResult xmlns:a="http://schemas.datacontract.org/2004/07/PDSRouting" xmlns:i="http://www.w3.org/2001/XMLSchema-instance"><a:AccountNumber>3528</a:AccountNumber><a:ResponseDateTime>2026-07-15T18:20:28.398788+00:00</a:ResponseDateTime><a:RateResponseEntries><a:RateResponseEntry><a:BoxID>0</a:BoxID><a:RateEntity><a:ProcessingCost>0</a:ProcessingCost><a:BillableWeight>1.11</a:BillableWeight><a:BillableWeightUOM>Pounds</a:BillableWeightUOM><a:DimensionFactor>139</a:DimensionFactor><a:FreightCost>22.92</a:FreightCost><a:InsureCost>0.00</a:InsureCost><a:LandedCost>0</a:LandedCost><a:CurrencyCode>USD</a:CurrencyCode></a:RateEntity><a:LandedCostDetailEntities><a:LandedCostDetailEntity><a:AdditionalCharge>0</a:AdditionalCharge><a:AdditionalChargeDescription i:nil="true"/><a:ProductCode>SUM-900200</a:ProductCode><a:Quantity>1</a:Quantity><a:UnitValue>12.99</a:UnitValue><a:DutyValue>0</a:DutyValue><a:VATValue>0</a:VATValue><a:HSTValue>0</a:HSTValue><a:PSTValue>0</a:PSTValue><a:GSTValue>0</a:GSTValue><a:CurrencyCode>USD</a:CurrencyCode></a:LandedCostDetailEntity></a:LandedCostDetailEntities><a:MessageEntities><a:MessageEntity><a:Code>00000</a:Code><a:Description>Request Successful</a:Description></a:MessageEntity></a:MessageEntities></a:RateResponseEntry></a:RateResponseEntries></RateLandedCostResult></RateLandedCostResponse></s:Body></s:Envelope>
```

Success-shape structure (per `RateResponseEntry`):

```
a:RateEntity                       — the rate figures
  ProcessingCost, BillableWeight, BillableWeightUOM, DimensionFactor,
  FreightCost, InsureCost, LandedCost, CurrencyCode
a:LandedCostDetailEntities         — one LandedCostDetailEntity per request line item
  AdditionalCharge, AdditionalChargeDescription, ProductCode, Quantity, UnitValue,
  DutyValue, VATValue, HSTValue, PSTValue, GSTValue, CurrencyCode
a:MessageEntities
  MessageEntity { Code 00000, Description "Request Successful" }
```

Cross-check vs the same package rated via GLP directly (glp-rate-live-capture-2026-07-15.md):
`FreightCost` 22.92 and `BillableWeight` 1.11 match exactly; legacy has **no**
`FuelSurcharge`/`TotalFreightCost`/`TotalCost`/`TotalTaxesDuties` elements (GLP's 5.73 fuel
surcharge and 28.65 total have no direct legacy slot), and duty/tax figures live per line item
(`DutyValue`, `VATValue`, `HSTValue`, `PSTValue`, `GSTValue`) instead of GLP's response-level
`DutyValue`/`VatValue`. Mapping GLP → legacy Result is therefore a real design decision, not a
rename.

## Plain `Rate` operation (valid WSKEY, captured 2026-07-15 20:03 UTC)

Same rData, `SOAPAction: "http://tempuri.org/IRouting/Rate"`, request element `<Rate>`:
`HTTP/1.1 200 OK`, 0.97 s.

```xml
<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/"><s:Body><RateResponse xmlns="http://tempuri.org/"><RateResult xmlns:a="http://schemas.datacontract.org/2004/07/PDSRouting" xmlns:i="http://www.w3.org/2001/XMLSchema-instance"><a:AccountNumber>3528</a:AccountNumber><a:ResponseDateTime>2026-07-15T20:03:28.2891184+00:00</a:ResponseDateTime><a:RateResponseEntries><a:RateResponseEntry><a:BoxID>0</a:BoxID><a:RateEntity><a:ProcessingCost>10</a:ProcessingCost><a:BillableWeight>1.11</a:BillableWeight><a:BillableWeightUOM>Pounds</a:BillableWeightUOM><a:DimensionFactor>139</a:DimensionFactor><a:FreightCost>22.92</a:FreightCost><a:InsureCost>0.00</a:InsureCost><a:LandedCost>0</a:LandedCost><a:CurrencyCode>USD</a:CurrencyCode></a:RateEntity><a:LandedCostDetailEntities i:nil="true"/><a:MessageEntities><a:MessageEntity><a:Code>00000</a:Code><a:Description>Request Successful</a:Description></a:MessageEntity></a:MessageEntities></a:RateResponseEntry></a:RateResponseEntries></RateResult></RateResponse></s:Body></s:Envelope>
```

Findings vs the `RateLandedCost` success capture:

- **Structure is identical** — `{Operation}Response > {Operation}Result` (`RateResult` here) with
  the same nested PDSRouting entries. The adapter's builder convention matches as-is.
- **⚠️ `ProcessingCost` is `10` on `Rate` but `0` on `RateLandedCost`** for the same package,
  account, and moment. The adapter currently emits a constant `0`, so hijacked plain-`Rate`
  responses under-report ProcessingCost by 10 vs legacy. GLP has no ProcessingCost figure to map;
  if the fee matters, it needs a per-operation constant/config or a GLP-side field. Business call
  pending.
- All other figures byte-identical (FreightCost 22.92, BillableWeight 1.11, DimensionFactor 139,
  InsureCost 0.00, LandedCost 0, USD, message 00000).

## Structure implications (⚠️ invalidates STUBBED MAPPING #2)

The real `<RateLandedCostResult>` is **nested and namespaced** (`a:` =
`http://schemas.datacontract.org/2004/07/PDSRouting`), mirroring the request's shape:

```
RateLandedCostResult
├── a:AccountNumber
├── a:ResponseDateTime
└── a:RateResponseEntries
    └── a:RateResponseEntry            (per package / BoxID)
        ├── a:BoxID
        ├── a:RateEntity               (nil here — presumably the rate figures on success)
        ├── a:LandedCostDetailEntities (nil here — presumably duty/tax detail on success)
        └── a:MessageEntities
            └── a:MessageEntity { a:Code, a:Description }
```

`OutboundFieldMap`'s flat PascalCase guess (`<FreightCost>` etc. directly under Result) is wrong.
The success-variant field names inside `RateEntity`/`LandedCostDetailEntities` are still unknown —
**this capture must be repeated with a valid WSKEY** to see them. Error shape is confirmed above
and the response builder also needs to produce `MessageEntities` for business errors.

Reproduce:

```sh
curl -sS -X POST 'http://pds.gdnparcel.com/Routing/Service/Soap/V2.6/Routing.svc' \
  -H 'Content-Type: text/xml; charset=utf-8' \
  -H 'SOAPAction: "http://tempuri.org/IRouting/RateLandedCost"' \
  --data-binary @rate-request-noheader.xml
```
