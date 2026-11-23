# 0007: Cursor (keyset) pagination everywhere

**Status:** accepted

## Context

Shelves are long, scrolled infinitely and change while being read. Offset paging repeats or skips rows when entries are added or removed.

## Decision

Lists return `{ items, nextCursor, totalCount }`. The cursor is opaque base64url JSON with the sort, the last row's sort value and its id; the query continues strictly after that position, using the id as a tie-breaker. Nullable sort keys sort as their lowest value. Search is the exception: its cursor wraps the provider's page number.

## Consequences

- Stable pages at any depth with an index-friendly query.
- No "jump to page 7"; the clients do not need it.
