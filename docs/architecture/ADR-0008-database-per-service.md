# ADR-0008: Database-per-Service

## Status

Accepted

## Context

In a microservices architecture, each service needs to store and manage its own data. The question is whether services should share a single database (with separate schemas or tables) or each own a completely independent database. This decision has profound implications for coupling, consistency, and operational complexity.

## Decision Drivers

- **Data isolation**: Services must not access each other's internal data structures
- **Independent deployability**: Schema changes in one service must not break others
- **Loose coupling**: The database should not become a hidden integration point between services
- **Consistency model**: Cross-service data must use explicit APIs or events, not shared tables
- **Operational simplicity**: Managing multiple databases adds overhead, but we accept this for educational purposes

## Considered Options

### Option 1: Shared Database

All services connect to a single SQL Server instance and database, using separate schemas (e.g., `identity.Users`, `wallet.Accounts`, `ledger.Entries`).

**Pros:**
- Single database to manage (backups, monitoring, connection strings)
- Cross-service queries are technically possible (even if discouraged)
- Simpler local development setup
- Lower resource usage

**Cons:**
- Schema coupling — a migration in one service's schema can affect shared infrastructure
- Temptation to bypass service boundaries via direct cross-schema queries
- Single point of failure — database outage affects all services
- Harder to scale individual service data stores independently
- Shared connection pool can become a bottleneck

### Option 2: Database-per-Service

Each service owns its own database (or logical database on the same SQL Server instance). No service has direct access to another service's database. Data is shared exclusively via published events and synchronous API calls.

**Pros:**
- Strong data isolation — services cannot accidentally or intentionally query each other's tables
- Independent schema evolution — each service migrates its own database
- Independent scaling — a high-traffic service's database can be scaled without affecting others
- Clear ownership — the service team owns the data model completely
- Aligns with microservices best practices and educational goals

**Cons:**
- No cross-service joins — data must be denormalized or fetched via API calls
- Eventual consistency for cross-service data views
- More databases to manage (backups, monitoring, connection strings)
- Reporting/analytics across services requires data aggregation
- Higher resource usage for local development (mitigated by using logical databases on a single SQL Server instance)

## Decision Outcome

**Chosen option: Database-per-service**, with logical databases on a single SQL Server instance for local development (via Docker Compose), simulating separate physical databases.

### Implementation Details

| Service | Database Name | Key Tables |
|---------|--------------|------------|
| Identity | `Kumbara.Identity` | Users, Roles, OpenIddictApplications, OpenIddictTokens |
| Wallet | `Kumbara.Wallet` | Accounts, AccountOwners |
| Ledger | `Kumbara.Ledger` | LedgerEntries, AccountBalances, OutboxMessages |
| Transfer | `Kumbara.Transfer` | Transfers, SagaStates, OutboxMessages |
| Risk | `Kumbara.Risk` | RiskAssessments, RiskRules |

- **EF Core migrations** are managed per service, in each service's Infrastructure project.
- **Connection strings** are configured per service via environment variables (see `.env.example`).
- **Docker Compose** runs a single SQL Server container; each service's `DbContext` targets its own database on that instance.
- **Data sharing** is done via:
  - **Integration events** (async, via RabbitMQ) — e.g., `UserRegistered` from Identity is consumed by Wallet to create an account
  - **Synchronous API calls** — e.g., Transfer service calls Risk service's API for assessment

## Consequences

### Positive

- Strict data isolation enforced at the database level
- Each service fully owns its data model and migration lifecycle
- Schema changes in one service cannot break others
- Clear demonstration of microservices data management patterns
- Forces use of events and APIs for cross-service data access (a key learning goal)

### Negative

- No cross-service SQL joins — queries that span services require API composition or materialized views
- Eventual consistency between services (e.g., Wallet's view of a user's balance may briefly lag behind Ledger's truth)
- Multiple EF Core migration sets to manage
- Local development requires either multiple database containers or multiple logical databases on one instance
- Reporting/analytics across the entire system requires a separate read store or API aggregation
