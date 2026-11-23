# Kunai API roadmap

Each step ends with a green build, passing tests and its own commit, so the repository is always in a working state.
If work stops midway, continue from the first unchecked step.

| # | Step | Status |
|---|---|---|
| 1 | Solution skeleton, quality gates, Aspire, CI | ✅ done |
| 2 | API conventions (problem details, validation, OpenAPI + Scalar, rate limiting) and test projects | ✅ done |
| 3 | Persistence (EF Core + PostgreSQL), registration, email confirmation | ✅ done |
| 4 | Sign-in, sessions, refresh tokens, logout (cookie for web, JWT for mobile) | ✅ done |
| 5 | Catalog model (6 item types) and the item-type registry endpoint | ✅ done |
| 6 | Metadata providers (TMDB, Google Books + Open Library, IGDB, AniList), search and import | ✅ done |
| 7 | Shelf endpoints | ✅ done |
| 8 | Profiles, settings, ninja avatars, stats, belts (ranks) and yearly goals (missions) | ✅ done |
| 9 | Account lifecycle (password reset/change, account deletion) and manual book moderation | ✅ done |
| 10 | Independent review and fixes | ✅ done |
| 11 | Documentation (README, ADRs, error catalogue, client integration guide) | ✅ done |

## Next

| Step | Notes |
|---|---|
| Publish the repository on GitHub | CI and Dependabot start working once it is pushed |
| Hosted environment | The container image is ready (`dotnet publish -t:PublishContainer`); needs a host (e.g. Azure Container Apps + Neon), a signing key, SMTP and a domain |
| Metadata refresh job | Re-sync imported items periodically (TMDB asks for cached data to be refreshed) |
| OpenAPI breaking-change check in CI | Commit the generated document and diff it on pull requests (oasdiff) |
| Web app | Blazor Web App calling the API through a client generated from the OpenAPI document |

## Deferred on purpose

Next Up queue, social features (follows, feed, reviews, lists), CSV imports (IMDb, Letterboxd, Goodreads), AI recommendations, image uploads, Google and Apple sign-in.
