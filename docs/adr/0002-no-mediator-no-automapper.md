# 0002: Plain handlers and hand-written mapping

**Status:** accepted

## Context

MediatR and AutoMapper became commercially licensed in 2025. Minimal APIs already provide what a mediator was mostly used for: dependency injection into handlers and filters for cross-cutting behavior.

## Decision

Endpoints call static handler methods directly, with dependencies injected as parameters. Responses are built with small `From(...)` methods and EF Core `Select` projections. Validation uses the built-in .NET 10 Minimal API validation with DataAnnotations and `IValidatableObject`.

## Consequences

- No license questions and no reflection-heavy libraries; stepping through code in a debugger goes straight to the handler.
- Mapping code is written by hand. It is short, and the compiler catches renamed fields.
