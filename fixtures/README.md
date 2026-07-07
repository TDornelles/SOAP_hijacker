# Fixtures — PLACEHOLDERS

These XML files are **placeholders**, not real Summit captures. They exist so the tests can run
end-to-end without Summit and without a live GLP.

Two things in them are **unconfirmed** and will change once we capture one real request/response
from Summit (see spec section 4):

1. The child element names inside `<rData>` (currently the camelCase DTO field names).
   Source of truth: `SummitAdapter/Soap/InboundFieldMap.cs` — `TODO(confirm)`.
2. The element names inside `<…Result>` (currently PascalCase landed-cost field names).
   Source of truth: `SummitAdapter/Soap/OutboundFieldMap.cs` — `TODO(confirm)`.

When a real capture arrives: update those two map files and replace these fixtures with the
captured XML. Nothing else should need to change.

| File | Operation | SOAPAction |
| --- | --- | --- |
| `rate-request.xml` | `RateLandedCost` | `http://tempuri.org/IRouting/RateLandedCost` |
| `rate-response.xml` | `RateLandedCost` | — |
| `route-request.xml` | `RouteDeliveryRateLandedCost` | `http://tempuri.org/IRouting/RouteDeliveryRateLandedCost` |
| `route-response.xml` | `RouteDeliveryRateLandedCost` | — |
