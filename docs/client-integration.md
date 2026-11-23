# Client integration guide

For the web app and the native mobile apps. The OpenAPI 3.1 document is at `/openapi/v1.json` and the interactive reference at `/docs`. Generate typed clients from the document rather than writing requests by hand:

| Client | Generator |
|---|---|
| iOS (Swift) | [swift-openapi-generator](https://github.com/apple/swift-openapi-generator) |
| Android (Kotlin) | [openapi-generator](https://openapi-generator.tech) with the `kotlin` generator |
| Blazor / .NET | Kiota, NSwag or Refitter |

All paths below are relative to `/api/v1`.

## Signing in

### Native apps: bearer tokens

1. `POST /auth/login` with `{ "login": "<email or username>", "password": "...", "deviceName": "iPhone 17" }`.
2. The response is `{ accessToken, tokenType: "Bearer", expiresIn: 900, refreshToken }`.
3. Send `Authorization: Bearer <accessToken>` on every request.
4. Before the access token expires (or after a `401`), call `POST /auth/refresh` with `{ "refreshToken": "..." }`. You get a **new** refresh token every time; store it and forget the old one.

Rules that matter:

- **Refresh tokens are single use.** Sending a used refresh token again is treated as theft and ends the whole session. Make sure only one refresh call runs at a time (serialize it behind a lock), and do not retry a refresh blindly after a timeout.
- Keep tokens in the Keychain (iOS) or EncryptedSharedPreferences / Keystore (Android), never in plain storage.
- `POST /auth/logout` ends this device's session; `POST /auth/logout-all` ends all of them.
- A session lasts 90 days at most and ends after 30 days without a refresh.

### Web app: cookie

1. `POST /auth/login` with `"useCookie": true`. The answer is `204` with an httpOnly `__Host-kunai` cookie.
2. Serve the web app and the API from the **same origin** (proxy `/api` to the API). The cookie is `SameSite=Lax`, `Secure` and host-only.
3. Every request that changes data (`POST`, `PUT`, `DELETE`) must send the header `X-CSRF: 1`; otherwise the API answers `403 csrf_header_missing`.

## Registration

`POST /auth/register` always answers `202` for a valid request, also when the email is already registered (the owner gets an email instead). The confirmation email links to `{web app}/confirm-email?userId=...&token=...`; the web app (or a universal link in the mobile app) calls `POST /auth/confirm-email` with those two values. Signing in before confirming answers `403 email_not_confirmed`.

Use `GET /auth/username-availability?username=` while the user types (debounced).

## Adding something to a shelf

1. `GET /search?q=dune&type=book` (at least 2 characters; debounce input by ~300 ms). Each hit has a `ref` and, if the item is already in the catalog, an `id`.
2. If `id` is null, call `POST /items/import` with `{ "ref": "<ref>" }`. It answers `201` (new) or `200` (already there) with the item. It is safe to retry.
3. `PUT /me/shelf/{itemId}` with the full entry:

```json
{
  "status": "completed",
  "rating": 9,
  "startedOn": "2026-11-01",
  "finishedOn": "2026-11-20",
  "progress": 412,
  "review": "Muhteşem.",
  "isFavorite": true
}
```

`PUT` replaces the whole entry: send every field, not only the changed ones.

If a book cannot be found, `POST /items` adds it by hand; it is visible only to its creator until a moderator approves it.

## Item types, statuses and labels

Do not hard-code status names or labels. `GET /item-types` (cacheable for an hour) lists, per type:

- the allowed statuses in display order, each with a Turkish and an English label (a movie has no "in progress"),
- the progress unit (`page`, `episode`, `chapter`, `percent`, or null),
- the belt points a completed item earns,
- the providers, plus `sources` with the attribution text you must show near provider data (TMDB, Google Books, IGDB, AniList).

## Dates and time zones

`startedOn` and `finishedOn` are **calendar dates in the user's own time zone** (`yyyy-MM-dd`). Fill them with the device's local date; the server never converts them. "This year" in profiles, stats and goals uses the time zone in `/me/settings` (default `Europe/Istanbul`). Timestamps ending in `At` are UTC instants.

## Concurrent edits (ETags)

`GET` and `PUT /me/shelf/{itemId}` return an `ETag`. Send it back as `If-Match` on the next `PUT`; if another device changed the entry in between, you get `412 precondition_failed`: reload and let the user decide. `If-None-Match: *` creates only. `GET` with `If-None-Match` answers `304` when nothing changed.

## Lists and paging

Lists use cursors: pass the previous page's `nextCursor` as `cursor`; `null` means the last page. Never build cursors yourself. Page size: `limit` 1-50, default 24. `/me/shelf` and `/users/{username}/shelf` filter by `type`, `status` (repeat the key for several), `favorite` and `year`, and sort by `-updatedAt` (default), `createdAt`, `finishedOn` or `rating` (prefix `-` for descending).

## Profiles, belts and avatars

- `GET /users/{username}`: counts, this year's completions, favorites and the belt (`rank`). Private profiles answer `404` to everyone except their owner.
- `GET /ranks`: the seven belts with names, thresholds and colors.
- `avatarUrl` is a relative URL such as `/api/v1/avatars/ninja-07.svg?belt=green`: the ninja's belt is painted in the user's current belt color. Users pick one of `ninja-01` ... `ninja-12` in `PUT /me/settings`.
- Yearly goals ("missions"): `PUT /me/goals/{year}` and `GET /users/{username}/goals/{year}`.

## Errors, retries and evolving the API

- Switch on the `code` of the problem response and translate it; see [errors.md](errors.md).
- On `429` and `503`, wait the number of seconds in `Retry-After`, then retry with backoff. Retry automatically only `GET`, `PUT`, `DELETE` and `/items/import`.
- **Be tolerant:** ignore JSON fields you do not know and map unknown enum values (for example a new item type) to an "unknown" case. New fields, endpoints and enum values are added without a new version.
- A breaking change gets a new version (`/api/v2`) and the old one keeps working for at least 90 days, announced with `Deprecation` and `Sunset` headers.
- Send a descriptive `User-Agent` (`KunaiiOS/1.4.0`) so problems can be traced to app versions.

## Account deletion

`DELETE /me` with `{ "password": "..." }` deletes the account and everything on it. Offer it inside the app; both app stores require that.
