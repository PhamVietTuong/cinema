# ADR-0005: Only the signed server-to-server callback can mark an invoice Paid

- **Date:** Author to confirm. First evidence in git: gateway seam in `3c92257` (2026-07-19); IPN contract made spec-compliant by `2df415b` (2026-10-23).
- **Status:** Proposed (draft reconstructed from the repository, not yet confirmed by the author)

## Context

- The customer's browser is redirected back from the gateway, but the browser is not trusted: a redirect URL can be forged or replayed.
- `AGENTS.md` states the rule: VNPay (and other gateways) are callback-authoritative; only the signed IPN or webhook can mark an invoice Paid, never the browser redirect. The IPN response must be exactly `{"RspCode": "..", "Message": ".."}` or VNPay retries for 50 minutes.
- In code: `VnPayGateway.VerifyPaymentAsync` always answers "not confirmed" (`cinemabe/Cinema/2-Business/Cinema.Business/Payments/VnPayGateway.cs:85-86`). `PaymentController.PaymentCallback` is `[AllowAnonymous]` and trusts only the HMAC-SHA512 signature, the order reference and the amount (`BookingManager.HandlePaymentCallbackAsync`, `business-rules.md` section 3).
- The frontend return page polls `GetInvoice` (2 seconds, 15 attempts) and shows "payment pending" if the IPN has not arrived (`payment-return.component.ts:18-19`).
- Before `2df415b` the IPN returned a generic body, so VNPay never saw success and retried, and the amount was not compared with the invoice.

## Decision

An invoice becomes `Paid` only through `PaymentCallback` to `HandlePaymentCallbackAsync`, after the gateway's own signature check and an amount check. The IPN answer for VNPay is the exact `RspCode`/`Message` JSON, HTTP 200 even on an internal error (`RspCode` 99), so the gateway does not retry in a storm. The callback is accepted only when the resolved gateway owns the invoice (`Invoice.PaymentMethod`).

Author to confirm: why this rule was adopted (for example VNPay's own requirement, or a security review) and who signed it off.

## Consequences

- A customer can see "payment pending" for a paid booking if VNPay cannot reach the server (the sandbox needs a public tunnel, `ngrok http 5102`, set in the VNPay portal each time it restarts).
- A payment confirmed after the invoice was cancelled by the reaper gets `RspCode` 02 and no ticket, which is why the reaper adds a 15-minute grace once a payment has started (`BookingManager.cs:1831`).
- Tests pin the contract: `PaymentControllerCallbackTests`, `VnPayGatewayTests.BuildIpnResponse_PinsTheExactRspCodeForEveryOutcome`, and the committed e2e spec `api-payment-callback.spec.ts` (`docs/05-process/testing.md`, rule BR-PAY-02).
- Idempotency and concurrency are handled by ADR-0008.

## Alternatives considered

- **Trusting the browser redirect or a synchronous confirm.** Still present for the Sandbox gateway only (`ConfirmPayment`, `SandboxPaymentGateway`), which is why the Sandbox is gated (ADR-0007); a real gateway's `VerifyPaymentAsync` always refuses.
- **A generic callback response for every provider.** What the code did before `2df415b`; VNPay never saw success and retried.
- **Other gateways.** Only VNPay and the dev Sandbox gateway sit behind the `IPaymentGateway` seam.
- Author to confirm: why polling the invoice on the return page was chosen over any other way of showing the result.
