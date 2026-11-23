# 0001: Vertical slices in one API project

**Status:** accepted

## Context

Kunai is built by one developer, may later be published, and needs to show good structure without ceremony. Clean Architecture with four projects per module spreads one use case over many folders; microservices would add network and deployment cost with no user-facing benefit.

## Decision

One `Kunai.Api` project organized by feature (`Features/Identity`, `Catalog`, `Library`, `Profiles`, `Moderation`). Each use case is one file with its endpoint mapping, handler and request/response types. Cross-cutting code that knows nothing about features lives in `Common`; the database context lives in `Persistence`. ArchUnitNET tests keep `Common` independent of features.

## Consequences

- A change usually touches one folder; reading a feature means reading one folder.
- Module boundaries are conventions checked by tests, not compiler-enforced project references. If a module ever needs to move out, it can be split into its own project then.
