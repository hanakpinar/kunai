# 0006: Problem details with stable codes; 400 versus 422

**Status:** accepted

## Context

Two clients in two languages (Turkish and English) must react to errors. English messages cannot be shown to users, and status codes alone are too coarse.

## Decision

- Every error is RFC 9457 problem details with a stable snake_case `code` and a `traceId`; validation errors list field codes under `errors`.
- 400: the request alone is wrong (validation, cross-field rules such as dates in order).
- 422: the request is well formed but breaks a rule that needs stored data (status not allowed for this item type, progress beyond the item's total).
- 404 also hides private or foreign resources, so their existence never leaks.
- Expected failures travel as `Result<T>`/`Error` values; exceptions are only for bugs and infrastructure faults.

## Consequences

- Clients translate codes and can show field-level messages. The catalogue lives in [docs/errors.md](../errors.md).
