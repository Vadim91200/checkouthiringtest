# Payment Gateway (.NET 8)

A small API that lets a merchant process a card payment through a simulated
acquiring bank and retrieve it afterwards.

```
POST /api/payments            process a payment  → 201 Authorized | 201 Declined | 400 Rejected | 502 bank unavailable
GET  /api/payments/{id}       retrieve a payment → 200 | 404
```

## Running it

.NET 8 SDK.

```bash
docker-compose up                                   # the bank simulator, on :8080
dotnet run --project src/PaymentGateway.Api         # the gateway (Swagger UI at /swagger in Development)
dotnet test                                         # the tests
```

The simulator's address is `Bank:BaseUrl` in `appsettings.json` (default
`http://localhost:8080`), overridable with the `Bank__BaseUrl` environment
variable.

`dotnet test` runs 40 tests with the bank faked, plus 3 end-to-end cases
through the real simulator that skip themselves when nothing listens on
`BANK_SIMULATOR_URL` (default `http://localhost:8080`), so the suite is green
with or without Docker.

### Example

```bash
curl -s localhost:5067/api/payments -H 'Content-Type: application/json' -d '{
  "cardNumber": "2222405343248877", "expiryMonth": 4, "expiryYear": 2027,
  "currency": "GBP", "amount": 100, "cvv": "123"
}'
# {"id":"9faf54e7-…","status":"Authorized","cardNumberLastFour":"8877",
#  "expiryMonth":4,"expiryYear":2027,"currency":"GBP","amount":100}

curl -s localhost:5067/api/payments/9faf54e7-…
```

## Layout

```
src/PaymentGateway.Api/
  Program.cs                          wiring: JSON options, the Rejected response, the bank HttpClient
  Controllers/PaymentsController.cs   the two endpoints
  Models/Requests/PostPaymentRequest.cs   every validation rule of the brief
  Models/Payment.cs                   what is stored and returned (last four only)
  Models/RejectedResponse.cs          the 400 body, built from ModelState
  Models/PaymentStatus.cs
  Services/IAcquiringBank.cs          the bank's contract + BankUnavailableException
  Services/BankClient.cs              the one HTTP call to the acquiring bank
  Services/PaymentsRepository.cs      in-memory store (the test double the brief allows)
test/PaymentGateway.Api.Tests/
  ValidationTests.cs                  one case per rule of the brief, bank never called, nothing echoed
  PaymentsApiTests.cs                 Authorized / Declined / bank down / retrieval / 404, with a fake bank
  BankClientTests.cs                  request shape and every simulator answer, on a stubbed handler
  SimulatorIntegrationTests.cs        the three card outcomes end to end (needs the simulator)
  Support/                            ApiFactory (real app, fake bank), FakeBank, request builder
```

No service layer: the flow is validate → ask the bank → store → answer, and
`ProcessPaymentAsync` reads exactly like that. The bank is behind
`IAcquiringBank` so the tests swap it for a fake through the normal DI
container (`ApiFactory`), without mocking frameworks.

## Design decisions and assumptions

**Rejected is a 400 with a structured body.** DataAnnotations on the request
model validate it; `[ApiController]`'s automatic 400 is redirected to
`RejectedResponse`: `{"status": "Rejected", "errors": [{"field", "message"}]}`.
The default ProblemDetails is not the brief's shape, and a custom body makes
sure nothing submitted is echoed back. A JSON type error (`"1050"` for an int)
is reported on its field too, without the CLR type name.

**The gateway never stores or returns the PAN or the CVV.** `Payment` holds
only the last four digits (as a string: `"0012"` keeps its zero), the expiry,
currency, amount and status. The template's `int CardNumberLastFour` and
`int Cvv` were replaced for the same reason.

**Declined is a created payment.** The bank made a decision, so the payment
gets an id, is stored and is retrievable like an authorized one. That is what
reconciliation needs. Both answer `201` with a `Location` header.

**Bank unavailable is `502`, nothing stored.** When the bank answers 503,
times out or cannot be reached, there is no authorization decision to report
and no payment to keep. The merchant may retry. `Rejected` stays reserved for
invalid input. A 400 from the bank is handled the same way, because it would
mean the gateway's own mapping is wrong, not the merchant's request.

**Validation rules, as the brief states them**, plus three choices it leaves
open:

- currencies: `GBP`, `USD`, `EUR` (the brief asks for no more than three);
- `amount` is strict: `"1050"` and `10.5` are rejected (`JsonNumberHandling.Strict`,
  the web default would read a number from a string), and so is `0`;
- expiry: the (year, month) pair must not be before the current UTC month. A
  card is valid through the last day of its expiry month, so a card expiring
  this month is accepted, which matches what an issuer does.

**Request properties are nullable** (`int?`, `string?`) so that an absent
field is reported as "required" rather than as an out-of-range `0`. They are
non-null once validation has passed, which is the only point they are read.

**Expiry is sent to the bank as `MM/YYYY`**, zero-padded, as the simulator's
contract shows; the bank's snake_case is a `JsonNamingPolicy`, not hand-written
attribute names.

**Ids are GUIDs** minted by the gateway; `{id:guid}` in the route makes a
malformed id a 404 like an unknown one.

**Repository** is a `ConcurrentDictionary`: the template's `List` +
`FirstOrDefault` returned null into a 200 for an unknown id.

**Dependencies.** ASP.NET Core and System.Text.Json only, plus Swashbuckle
from the template. Test packages were bumped to current versions
(`Mvc.Testing` 8.0 to match the target framework). `UseHttpsRedirection` and
`UseAuthorization` were dropped: there is no certificate in the simulator
setup and no auth in the brief.

## What I would do next

- **Idempotency key** on `POST /api/payments`: a merchant retrying after a
  timeout must not charge the shopper twice. This is the first thing a real
  gateway needs and the reason the bank-unavailable path stores nothing today.
- **Merchant authentication** and scoping of `GET /api/payments/{id}` to the
  merchant who created it.
- **Persist the bank's `authorization_code`** once a capture or refund flow
  needs it; the client reads it and discards it.
- **Timeout on the bank call is 5 s and there is no retry.** A retry on a
  timeout without idempotency on the bank side could double-authorize.
- Replace the in-memory repository with a real store behind the same
  two-method class.
