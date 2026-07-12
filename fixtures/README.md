# Fixtures

**`rate-request.xml` is a REAL Summit capture** (transcribed 2026-07-12 from screenshots in
`GDN_dump/summit_requests`; the live WSKEY replaced with a dummy GUID). It is the ground truth for
inbound mapping #1: rData is nested — header fields, then
`RatePackageRequests > RatePackageRequest`, with `RatePackageDetailRequests` line items inside.
`SoapRequestParser` parses this exact shape; the parser tests assert against this file.

The other XML files are **placeholders**:

- `route-request.xml` — no real Route capture yet. Mirrors the confirmed nested rData style, but the
  Route-specific fields (consignee address etc.) are unknown until captured. Only used for
  pass-through tests today (pass-through never parses the body).
- `rate-response.xml` / `route-response.xml` — **the response side has never been captured.**
  Outbound mapping #2 (`<…Result>` element names) is still the stubbed guess in
  `SummitAdapter/Soap/OutboundFieldMap.cs` — `TODO(confirm)` stands until a real response capture
  (or WSDL) arrives.

| File | Operation | SOAPAction | Real? |
| --- | --- | --- | --- |
| `rate-request.xml` | `RateLandedCost` | `http://tempuri.org/IRouting/RateLandedCost` | **YES** (WSKEY redacted) |
| `rate-response.xml` | `RateLandedCost` | — | placeholder |
| `route-request.xml` | `RouteDeliveryRateLandedCost` | `http://tempuri.org/IRouting/RouteDeliveryRateLandedCost` | placeholder (real nested style) |
| `route-response.xml` | `RouteDeliveryRateLandedCost` | — | placeholder |
