# Error codes

Every error response is `application/problem+json` ([RFC 9457](https://www.rfc-editor.org/rfc/rfc9457)) with two extra fields:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "One or more fields are invalid.",
  "code": "validation_failed",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "errors": { "rating": ["rating_out_of_range"] }
}
```

- **`code`** is stable. Switch on it and translate it in the client; `title` and `detail` are English and meant for developers.
- **`traceId`** identifies the request in logs and traces. Include it in bug reports.
- **`errors`** appears on validation problems: JSON field name (camelCase) to a list of field codes.

## Status codes

| Status | Meaning |
|---|---|
| 400 | The request alone is wrong: validation, malformed JSON, bad cursor |
| 401 | Not signed in, or the session ended |
| 403 | Signed in but not allowed (also: missing `X-CSRF` header on a cookie request) |
| 404 | Not found, **including** private profiles and other users' data, so their existence never leaks |
| 409 | Conflict with existing data (taken username, duplicate ISBN) |
| 412 | `If-Match` / `If-None-Match` precondition failed |
| 422 | The request is well formed but breaks a business rule that needs stored data |
| 429 | Rate limited; wait `Retry-After` seconds |
| 503 | A metadata provider is down; wait `Retry-After` seconds |

## Codes

### General

| Code | Status | When |
|---|---|---|
| `validation_failed` | 400 | One or more fields are invalid; see `errors` |
| `bad_request` | 400 | The body or query could not be read |
| `invalid_cursor` | 400 | The `cursor` is not one this endpoint issued for this sort |
| `unauthorized` | 401 | No credentials, or the session ended |
| `forbidden` | 403 | Not allowed |
| `csrf_header_missing` | 403 | Cookie-authenticated write without the `X-CSRF` header |
| `not_found` | 404 | No such route or resource |
| `method_not_allowed` | 405 | Wrong HTTP method |
| `conflict` | 409 | Generic conflict |
| `precondition_failed` | 412 | ETag precondition failed |
| `unsupported_media_type` | 415 | Send `application/json` |
| `business_rule_violated` | 422 | Generic business rule |
| `rate_limited` | 429 | Too many requests |
| `internal_error` | 500 | A bug; the `traceId` helps find it |
| `service_unavailable` | 503 | Temporarily unavailable |

### Accounts and sign-in

| Code | Status | When |
|---|---|---|
| `username_invalid` | 400 (field) | Not 3-24 lowercase letters, digits or underscores starting with a letter |
| `username_reserved` | 400 (field) | Reserved name such as `admin` or `me` |
| `username_taken` | 409 | Someone already has it |
| `username_change_cooldown` | 422 | Usernames change at most once every 30 days |
| `password_length` | 400 (field) | Not 12-128 characters |
| `password_too_short` | 400 (field) | Shorter than the policy |
| `password_breached` | 400 (field) | Found in known data breaches |
| `password_rejected` | 400 (field) | Rejected by another password rule |
| `password_mismatch` | 400 (field) | The current password is wrong |
| `invalid_or_expired_token` | 400 | Confirmation or reset link is invalid, used or expired |
| `invalid_credentials` | 401 | Wrong login or password |
| `email_not_confirmed` | 403 | Confirm the email before signing in |
| `account_locked` | 403 | 5 failed attempts; try again in 15 minutes |
| `invalid_refresh_token` | 401 | Unknown, expired or reused refresh token; sign in again |

### Catalog and search

| Code | Status | When |
|---|---|---|
| `query_length` | 400 (field) | Search text shorter than 2 or longer than 100 characters |
| `type_invalid` | 400 (field) | Not one of book, movie, series, game, anime, manga |
| `ref_invalid` | 400 (field) | Not a search result `ref` such as `tmdb:movie:603` |
| `item_not_found` | 404 | No such item, or a manual item that is not approved yet |
| `external_item_not_found` | 422 | The provider does not know this id |
| `provider_unavailable` | 503 | The provider is not answering |
| `provider_not_configured` | 503 | The server has no credentials for this provider |
| `isbn_invalid` | 400 (field) | Not a valid ISBN-13 |
| `book_exists` | 409 | A book with this ISBN is already in the catalog |
| `manual_item_limit` | 422 | At most 10 manually added books a day |
| `item_not_pending` | 409 | The item was already approved or rejected |

### Shelf

| Code | Status | When |
|---|---|---|
| `rating_out_of_range` | 400 (field) | Rating outside 1-10 |
| `progress_out_of_range` | 400 (field) | Negative or absurd progress |
| `finished_before_started` | 400 (field) | `finishedOn` is before `startedOn` |
| `status_invalid` | 400 (field) | Unknown status in a filter |
| `sort_invalid` | 400 (field) | Unknown sort |
| `status_not_allowed` | 422 (field) | The status does not exist for this type (e.g. a movie "in progress") |
| `planned_has_no_activity` | 422 (field) | Planned entries have no rating, dates or progress |
| `progress_not_tracked` | 422 (field) | This type has no progress (movies) |
| `progress_exceeds_total` | 422 (field) | More pages/episodes/chapters than the item has, or more than 100% |
| `shelf_entry_not_found` | 404 | The item is not on your shelf |
| `concurrent_change` | 409 | The entry was created by another request at the same moment |

### Profiles and goals

| Code | Status | When |
|---|---|---|
| `user_not_found` | 404 | No such user, or the profile is private |
| `time_zone_invalid` | 400 (field) | Not an IANA time zone such as `Europe/Istanbul` |
| `avatar_invalid` | 400 (field) | Not one of `ninja-01` ... `ninja-12` |
| `avatar_not_found` | 404 | No such avatar |
| `locale_invalid` | 400 (field) | Not `tr` or `en` |
| `duplicate_type` | 400 (field) | The same type appears twice in goal targets |
| `target_out_of_range` | 400 (field) | Goal target outside 1-10000 |

### Field codes shared by many endpoints

`required`, `too_long`, `too_many`, `email_invalid`, `title_length`, `display_name_length`, `year_out_of_range`, `page_count_out_of_range`, `limit_out_of_range`.
