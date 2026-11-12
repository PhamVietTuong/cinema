# ADR-0007: The Sandbox payment gateway exists only in Development or when explicitly enabled

- **Date:** 2026-11-11 (`0f83416`). Author to confirm that this is the decision date.
- **Status:** Proposed (draft reconstructed from the repository, not yet confirmed by the author)

## Context

- `SandboxPaymentGateway` approves unsigned callbacks and synchronous confirms. It exists so a developer can book without a real merchant account.
- Until `0f83416` the gateway was registered in every environment (introduced by `3c92257`, 2026-07-19). An unsigned Sandbox callback could therefore reach a production host, and a callback or confirm was not required to come from the gateway that owns the invoice.
- Now `Program.cs` registers it only when `builder.Environment.IsDevelopment()` or the setting `Payments:EnableSandbox` is `true` (`cinemabe/Cinema/1-Service/Cinema.Service.WebApiHost/Program.cs:54-62`). `docs/03-technical/configuration.md` documents the setting (default `false`).
- `HandlePaymentCallbackAsync` and `ConfirmPaymentAsync` require the resolved gateway to own the invoice, so an unsigned Sandbox callback can never finalize a VNPay invoice.
- Two places turn it on outside Development on purpose: the CI e2e job (`.github/workflows/ci.yml:198`) and the docker-compose demo stack (`Payments__EnableSandbox: "true"`).

## Decision

The Sandbox gateway is off by default and must be switched on by configuration. Gateway ownership of an invoice is checked on every callback and confirm, in addition to the signature.

Author to confirm: whether the compose demo should keep the Sandbox on, and whether a startup warning or refusal is wanted when it is enabled together with `ASPNETCORE_ENVIRONMENT=Production`.

## Consequences

- A Production host with the default configuration has no Sandbox gateway; the e2e and compose setups must set the switch, and the docs say never to set it on a real deployment.
- Regression tests: `PaymentCallbackTests.HandlePaymentCallbackAsync_SandboxCallbackOnVnPayInvoice_IsRejected_NoWrites`, `ConfirmPaymentAsync_InvoiceOwnedByUnavailableProvider_DoesNotFallBackToSandbox`.
- The gateway resolver never hands out an unconfigured gateway (`6c31db5`), so a missing VNPay configuration yields a provider-specific error.
- This was found by the audit, not by a user report; see defect 1 in `docs/05-process/ai-usage-log.md`.

## Alternatives considered

- **Always register the Sandbox gateway.** The state until `0f83416`; it let an unsigned callback reach a production host.
- **Never register it, and test with a stub.** Not shown in the repository. Author to confirm: whether it was considered; CI e2e and the compose demo rely on the Sandbox being available behind the switch.
- **Select it by `Payments:Provider` alone.** That setting picks the default gateway only (`docs/03-technical/configuration.md`); it does not register one.
