# ADR-0006: Idempotency Strategy

## Status

Accepted

## Context

Kumbara uses at-least-once delivery for events (via the transactional outbox, see [ADR-0005](ADR-0005-outbox-pattern.md)). This means consumers may receive the same event more than once. Additionally, API clients may retry failed HTTP requests due to network timeouts, sending the same mutation multiple times.

Without idempotency handling, duplicate processing can cause:
- Double debits/credits in the ledger
- Duplicate wallet creations
- Multiple risk assessments for the same transfer

## Decision Drivers

- **Correctness**: Processing the same request twice must produce the same result as processing it once
- **Universality**: The pattern should work for both HTTP API calls and async message consumers
- **Transparency**: Callers should not need to know if their request was already processed — they get the same response
- **Performance**: Idempotency checks should be fast and not significantly degrade throughput
- **TTL**: Stored idempotency keys should expire after a reasonable period to prevent unbounded growth

## Considered Options

### Option 1: Natural Idempotency (Design Each Operation to Be Naturally Idempotent)

Design every operation so that repeating it has no additional effect. For example, "set balance to 100" is naturally idempotent; "add 100 to balance" is not.

**Pros:**
- No additional infrastructure
- Elegant when possible

**Cons:**
- Not always possible — many financial operations are inherently non-idempotent (transfers, ledger entries)
- Pushes complexity into every operation's design
- No generic solution for API retries

### Option 2: Idempotency-Key Header with Response Caching

Require an `Idempotency-Key` header on all mutating API endpoints. Store the key and the response in the database. On duplicate requests (same key), return the cached response without re-processing.

**Pros:**
- Generic solution — works for any mutating endpoint
- Transparent to callers (same response on retry)
- Simple to implement as middleware
- Well-established pattern (Stripe, PayPal, etc.)

**Cons:**
- Requires storage for keys + responses (mitigated by TTL)
- Clients must generate and send unique keys
- Adds a DB lookup on every mutating request

## Decision Outcome

**Chosen option: Idempotency-Key header with response caching**, implemented as ASP.NET Core middleware in the `BuildingBlocks.Idempotency` package.

### Implementation Details

#### HTTP API Idempotency

1. **All mutating endpoints** (`POST`, `PUT`, `PATCH`) require an `Idempotency-Key` header (UUID).
2. Middleware flow:
   ```
   Request arrives with Idempotency-Key: "abc-123"
       │
       ├─ Key exists in store?
       │   ├─ YES → Return cached response (status code + body)
       │   └─ NO  → Process request normally
       │            ├─ Store key + response in DB (same transaction if possible)
       │            └─ Return response
   ```
3. Keys are stored in an `IdempotencyKey` table:
   ```
   IdempotencyKey
   ├── Key           : string (PK)
   ├── StatusCode    : int
   ├── ResponseBody  : string (JSON)
   ├── CreatedAt     : datetime2
   └── ExpiresAt     : datetime2 (CreatedAt + TTL)
   ```
4. **TTL**: Keys expire after 24 hours. A background job purges expired keys.
5. Missing `Idempotency-Key` on a mutating endpoint returns `400 Bad Request`.

#### Message Consumer Idempotency

1. MassTransit's inbox/outbox (`InboxState` table) tracks consumed message IDs.
2. If a message with the same `MessageId` arrives again, MassTransit skips processing.
3. This is configured via `AddEntityFrameworkOutbox` with `cfg.UseInMemoryInboxStateRepository()` or the EF Core inbox.

#### Client Responsibility

Clients must:
- Generate a unique `Idempotency-Key` (UUID v4) for each logical operation
- Reuse the same key when retrying the same operation
- Never reuse a key for a different operation

## Consequences

### Positive

- Duplicate HTTP requests are safe — same key returns the same response
- Duplicate messages are safe — MassTransit inbox deduplicates
- Generic middleware — no per-endpoint idempotency logic needed
- Aligns with industry standard (Stripe, PayPal, Google Pay APIs)
- TTL prevents unbounded storage growth

### Negative

- Clients must generate and manage idempotency keys (additional client-side logic)
- Every mutating request incurs one DB read (key lookup) — typically negligible
- Cached responses consume storage (mitigated by TTL and cleanup)
- Race condition window: two simultaneous requests with the same key may both pass the "key exists?" check — mitigated by unique constraint on the key column
