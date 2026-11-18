# ADR-0002: Ledger Strong Consistency Model

## Status

Accepted

## Context

The Ledger service is the financial core of Kumbara. It records all monetary movements as ledger entries. The fundamental invariant is: **money must never be lost, created from nothing, or double-spent**. Even though Kumbara uses simulated money, the ledger must behave as if it were real to serve its educational purpose.

In a microservices architecture, achieving global consistency across services is impractical. We need to define where strong consistency is required and where eventual consistency is acceptable.

## Decision Drivers

- **Financial correctness**: Debit and credit entries for a transaction must both succeed or both fail — never one without the other
- **Double-spend prevention**: An account must not be able to spend more than its balance
- **Auditability**: Every monetary movement must be traceable
- **Performance**: Strong consistency has a throughput cost; we need to scope it carefully
- **Distributed architecture**: Cross-service transactions cannot rely on distributed ACID

## Considered Options

### Option 1: Strong Consistency Everywhere (Distributed Transactions / 2PC)

Use distributed transactions (two-phase commit) across all services involved in a transfer.

**Pros:**
- Global ACID guarantees
- Simpler mental model — everything is consistent at all times

**Cons:**
- Extremely poor performance and scalability
- Tight coupling between services (all must be available)
- 2PC is fragile in practice (coordinator failure, participant timeout)
- Not supported by most message brokers
- Defeats the purpose of microservices

### Option 2: Strong Consistency Within Ledger + Eventual Consistency Between Services

Apply strong consistency (single database transaction) within the Ledger service for debit/credit entry pairs. Use eventual consistency between services via saga orchestration and the transactional outbox pattern.

**Pros:**
- Financial correctness guaranteed where it matters most (ledger entries)
- Services remain loosely coupled
- Saga compensations handle cross-service failures
- Scalable — each service manages its own consistency boundary
- Aligns with real-world fintech architectures

**Cons:**
- Temporary inconsistency windows between services
- Saga compensation logic adds complexity
- Read models may show stale data briefly
- Requires idempotent consumers

## Decision Outcome

**Chosen option: Strong consistency within Ledger, eventual consistency between services.**

### Implementation Details

1. **Double-entry bookkeeping**: Every transaction creates exactly two `LedgerEntry` records — one debit and one credit — that sum to zero. Both entries are written in a single database transaction.

2. **Within the Ledger service**: A single SQL Server transaction wraps:
   - Validation of sufficient balance
   - Creation of debit entry (negative amount)
   - Creation of credit entry (positive amount)
   - Update of cached account balances (with optimistic concurrency via `rowversion`)
   - Writing the outbox event

3. **Between services**: The Transfer service orchestrates the flow via a MassTransit saga:
   - Risk assessment → Ledger hold → Ledger commit (or compensate)
   - Each step publishes events via the transactional outbox
   - Eventual consistency is achieved when all saga steps complete

4. **Append-only ledger**: No `UPDATE` or `DELETE` on `LedgerEntry` records. See [ADR-0003](ADR-0003-double-entry-append-only.md).

## Consequences

### Positive

- Absolute guarantee that debit + credit entries are always paired
- No money can be lost or created from nothing within the ledger boundary
- Balance validation under optimistic locking prevents double-spend
- Outbox in the same transaction ensures events are published if and only if the ledger change commits
- Realistic model of how actual payment systems work

### Negative

- Temporary windows where the Transfer service state and Ledger state diverge
- Wallet service balance view may lag behind actual ledger balance
- Compensation flows (refunds) add entries rather than rolling back — balance may temporarily show holds
- Optimistic locking on `AccountBalance` creates contention under high concurrency on the same account
