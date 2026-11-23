# 0004: PostgreSQL, one items table, a non-Turkish database locale

**Status:** accepted

## Context

Six item types share most fields (title, year, cover, creators, genres, progress total) and differ in a few (ISBN, runtime, platforms, format). Users are mostly Turkish, and Turkish casing (I/ı, İ/i) breaks naive case-insensitive comparisons.

## Decision

- PostgreSQL 18 with EF Core 10 and snake_case names; a single `AppDbContext`.
- Items use table-per-hierarchy: one `items` table with a `type` discriminator and nullable type-specific columns; creators and genres are `text[]`.
- Ids are UUIDv7, generated in code. Enums are stored as strings. Row versions use PostgreSQL `xmin`.
- Databases are created with the `C.UTF-8` locale (Aspire, Testcontainers and hosting alike), never a Turkish one. In C#, culture-sensitive string APIs are build errors.

## Consequences

- Adding a type means a subclass, a registry entry and a small migration.
- Titles do not sort in Turkish alphabetical order by default; that needs an ICU collation when title sorting is added.
