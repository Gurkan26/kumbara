# ADR-0005: Transactional Outbox Pattern

## Status

Accepted

## Context

In a microservices architecture with event-driven communication, services often need to both update their database and publish an event to the message broker. This creates the **dual-write problem**:

1. If we update the DB first and then publish the event, a crash between the two leaves the event unpublished — other services never learn about the change.
2. If we publish the event first and then update the DB, a crash between the two means we published an event for a change that never persisted.

Neither ordering guarantees atomicity without an additional pattern.

## Decision Drivers

- **Atomicity**: DB change and event publishing must be all-or-nothing
- **Reliability**: Events must not be lost, even if the message broker is temporarily down
- **Simplicity**: The pattern should be understandable and implementable without exotic infrastructure
- **At-least-once guarantee**: It is acceptable to publish an event more than once (consumers handle duplicates), but never zero times
- **Framework support**: MassTransit has built-in support for the outbox pattern with EF Core

## Considered Options

### Option 1: Publish Directly After DB Commit

After committing the database transaction, publish the event to RabbitMQ in application code.

**Pros:**
- Simple — no additional infrastructure
- Low latency (event published immediately)

**Cons:**
- Not atomic — crash after DB commit but before publish loses the event
- Broker downtime causes event loss unless manual retry logic is added
- No guaranteed delivery

### Option 2: Transactional Outbox

Write the event to an `OutboxMessage` table in the **same database transaction** as the business data change. A background process (OutboxPublisher) polls or listens for unpublished messages and relays them to RabbitMQ.

**Pros:**
- Atomic — event is persisted if and only if the business change commits
- Resilient to broker downtime — events are stored in the DB until published
- At-least-once delivery guarantee
- Well-supported by MassTransit's EF Core outbox integration

**Cons:**
- Slight delay between DB commit and event publication (polling interval)
- Requires an outbox table in every service's database
- Consumers must be idempotent (at-least-once means possible duplicates)
- Additional DB writes per operation

### Option 3: Change Data Capture (CDC)

Use database change data capture (e.g., Debezium) to stream changes from the DB transaction log to the message broker.

**Pros:**
- No application-level outbox code
- Very low latency
- Works with any database change, not just explicitly published events

**Cons:**
- Requires additional infrastructure (Debezium, Kafka Connect)
- Complex to set up and operate
- Overkill for this project's scale
- Harder to control event schema (derived from DB schema)

## Decision Outcome

**Chosen option: Transactional Outbox**, using MassTransit's built-in EF Core outbox integration.

### Implementation Details

1. **OutboxMessage table** is added to each service's EF Core `DbContext` via MassTransit's `AddEntityFrameworkOutbox<TDbContext>()`.

2. **Business operation + outbox write** occur in a single `SaveChangesAsync()` call:

   ```csharp
   await using var transaction = await dbContext.Database.BeginTransactionAsync();

   // Business logic
   dbContext.LedgerEntries.Add(debitEntry);
   dbContext.LedgerEntries.Add(creditEntry);

   // Event is written to OutboxMessage table, NOT published to RabbitMQ yet
   await publishEndpoint.Publish(new LedgerEntriesCreated { ... });

   await dbContext.SaveChangesAsync();
   await transaction.CommitAsync();
   ```

3. **OutboxPublisher** (MassTransit's `BusOutboxDeliveryService`) runs as a background hosted service, polling the outbox table and publishing messages to RabbitMQ. Once published, messages are marked as delivered.

4. **At-least-once**: If the publisher crashes after publishing but before marking as delivered, the message will be re-published on restart. Consumers must be idempotent (see [ADR-0006](ADR-0006-idempotency-strategy.md)).

## Consequences

### Positive

- Guaranteed atomicity between DB changes and event publishing
- Events survive broker downtime (stored in DB until delivered)
- MassTransit handles the outbox plumbing — minimal custom code
- Aligns with industry-standard patterns for reliable event-driven systems
- Naturally forces idempotent consumer design

### Negative

- Small latency between DB commit and event availability on the broker (typically < 1 second with MassTransit's default polling)
- Each service's database has additional outbox tables (OutboxMessage, OutboxState, InboxState)
- Database size grows with event volume (mitigated by cleanup of delivered messages)
- Consumers must handle duplicate events (see [ADR-0006](ADR-0006-idempotency-strategy.md))
