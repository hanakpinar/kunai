# Architecture decision records

Short records of the decisions that shape the codebase: the context, the choice and what it costs.

| # | Decision |
|---|---|
| [0001](0001-vertical-slices.md) | Vertical slices in one API project |
| [0002](0002-no-mediator-no-automapper.md) | Plain handlers and hand-written mapping |
| [0003](0003-cookie-and-token-sessions.md) | Cookie for the web, rotating tokens for mobile, one session model |
| [0004](0004-postgres-tph-and-locale.md) | PostgreSQL, one items table, a non-Turkish database locale |
| [0005](0005-import-on-demand.md) | Import metadata on demand from providers |
| [0006](0006-errors-and-status-codes.md) | Problem details with stable codes; 400 versus 422 |
| [0007](0007-keyset-pagination.md) | Cursor (keyset) pagination everywhere |
