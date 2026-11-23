# 0005: Import metadata on demand from providers

**Status:** accepted

## Context

Copying whole catalogs is not allowed by most providers and not needed. Users search, then pick one result.

## Decision

- Search goes to the provider for the requested type (TMDB, Google Books then Open Library, IGDB, AniList) and caches successful pages for 12 hours.
- `POST /items/import` creates the catalog item from the provider's detail response the first time anyone adds it. A unique index on `(source, external_id)` plus a re-read makes concurrent imports converge on one row, so the endpoint is idempotent.
- Each provider has its own HttpClient with its own resilience pipeline and, where the provider asks for it, a client-side rate limit. Failures become `503 provider_unavailable` with `Retry-After`, never exceptions.
- Books that no provider knows can be added by hand and are moderated.

## Consequences

- The catalog grows with what users actually track.
- Provider terms limit how long data may be cached; a periodic refresh job is planned before public launch.
