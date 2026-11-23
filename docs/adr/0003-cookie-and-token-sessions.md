# 0003: Cookie for the web, rotating tokens for mobile, one session model

**Status:** accepted

## Context

The web app runs in a browser, where tokens in JavaScript are exposed to XSS. Native apps have secure storage and need long-lived sign-in. ASP.NET Core's `MapIdentityApi` has no usernames, no revocation and proprietary tokens; an external identity provider would hide exactly the backend skills this project shows.

## Decision

- ASP.NET Core Identity stores users and passwords.
- Web: `POST /auth/login` with `useCookie` sets an httpOnly, Secure, SameSite=Lax `__Host-` cookie. Writes need an `X-CSRF` header.
- Mobile: a 15-minute ES256 JWT plus an opaque refresh token. Refresh tokens are single use and stored as SHA-256 hashes; replaying a used token revokes the session.
- Both carry a `sid` claim pointing at a `user_sessions` row. A session guard (cached for 60 seconds) rejects revoked sessions, so logout takes effect immediately for cookies and tokens alike.
- Every endpoint requires sign-in unless it explicitly allows anonymous access.

## Consequences

- One revocation mechanism for both clients; "sign out all devices" and password reset invalidate everything.
- One database lookup per minute per session, plus a small risk window of up to 60 seconds on other instances when scaled out.
- If third-party apps ever need to act for users, OpenIddict (OAuth 2.0) is the upgrade path.
