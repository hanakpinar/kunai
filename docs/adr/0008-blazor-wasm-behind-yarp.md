# 0008: Blazor WebAssembly behind a same-origin proxy, with a generated client

**Status:** accepted

## Context

The web app is a client of the public API like the mobile apps, not a second backend. The API signs the web in with an httpOnly `__Host-` cookie (see [0003](0003-cookie-and-token-sessions.md)), which only works when the browser sees the API on the same origin as the page. The author wants to learn Blazor and keep the whole stack in C#.

Options considered:

- **Blazor Server:** every click is a SignalR round trip and the server holds a circuit per tab; the server would also have to forward the user's cookie on each API call.
- **Standalone WebAssembly on its own origin:** the cookie becomes cross-site (SameSite, CORS with credentials, third-party cookie blocking).
- **Blazor Web App with interactive WebAssembly and a reverse proxy:** the page and `/api` share one origin.

## Decision

- `Kunai.Web` is a thin host: it serves the WebAssembly app (`Kunai.Web.Client`) and forwards `/api/**` to the API with YARP. In Aspire the destination comes from service discovery (`https+http://api`); elsewhere from `Api:BaseUrl`. It holds no state and no secrets.
- The API's cookie passes through unchanged and becomes a first-party cookie of the web origin. The client adds `X-CSRF: 1` to every write.
- Pages render only in the browser (`prerender: false`). Almost every page depends on the signed-in user, and prerendering would mean calling the API twice, once from the server with a forwarded cookie.
- The host serves the same shell for every page route and maps them as anonymous: `[Authorize]` on a page is checked in the browser (redirect to `/login`), and the API protects the data. Without this, a reload of a protected page made the host try to challenge a user it cannot know and answer 500; a host test now loads every route directly.
- The typed client is generated with Kiota from the committed contract `openapi/kunai.v1.json` (`scripts/generate-web-client.sh`). A test fails when the API's live document drifts from the committed one, so the contract changes in the same pull request as the code.
- API errors become Turkish messages in one place (`ApiProblem` + `ErrorMessages`), keyed by the problem details `code`; status labels come from the API's item-type registry instead of being hard-coded.
- Components are tested with bUnit against a fake `HttpMessageHandler`, so the tests go through the generated client and the real JSON shapes.

## Consequences

- No CORS, no tokens in browser storage; the web uses exactly the cookie flow documented for clients.
- One extra hop per API call, and the first visit downloads the .NET WebAssembly runtime (cached afterwards).
- Public profiles are not prerendered, so they are invisible to crawlers that do not run JavaScript. Prerendering only `/u/{username}` is the upgrade path if that matters.
- The generated client (about 100 files) is checked in and excluded from analyzers; it is regenerated, never edited.
