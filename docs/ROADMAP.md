# Kunai roadmap

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

## Web app

| # | Step | Status |
|---|---|---|
| W1 | Blazor Web App host with a YARP proxy to the API, wired into Aspire | ✅ done |
| W2 | Committed OpenAPI contract, contract test and the Kiota-generated client | ✅ done |
| W3 | Session, layout, design system, shared components, bUnit test harness | ✅ done |
| W4 | Account pages: sign-in, registration, email confirmation, password reset | ✅ done |
| W5 | Search and item pages, manual books, shelf, public profile (stats, missions, belts), settings | ✅ done |

## Next

| Step | Notes |
|---|---|
| Hosted environment | Container images for the API and the web host; needs a host (e.g. Azure Container Apps + Neon), a signing key, SMTP and a domain |
| Metadata refresh job | Re-sync imported items periodically (TMDB asks for cached data to be refreshed) |
| OpenAPI breaking-change check in CI | The contract is committed and tested; add an oasdiff check on pull requests |
| Browser end-to-end tests | Playwright against the Aspire app host |
| Prerender public profiles | Only if search engines should see `/u/{username}` |

## Deferred on purpose

Next Up queue, social features (follows, feed, reviews, lists), CSV imports (IMDb, Letterboxd, Goodreads), AI recommendations, image uploads, Google and Apple sign-in.
