# ADR-0003: Double-Entry Append-Only Ledger

## Status

Accepted

## Context

The Ledger service must record all financial movements accurately and provide a full audit trail. We need to decide how ledger entries are stored and how balances are computed. Traditional mutable-row approaches (updating a balance column directly) are simpler but sacrifice auditability. The choice directly impacts how error corrections, refunds, and balance queries work.

## Decision Drivers

- **Auditability**: Every monetary movement must be traceable; regulators (even simulated ones) expect a complete history
- **Correctness**: The source of truth for a balance must be mathematically provable
- **Error handling**: Mistakes happen — the system must correct errors without destroying evidence
- **Performance**: Balance queries must be fast; summing all entries on every query is not feasible at scale
- **Concurrency**: Multiple transfers to/from the same account must not corrupt the balance

## Considered Options

### Option 1: Mutable Balance with Transaction Log

Store the current balance in an `Account` table row. Update it in place on each transaction. Keep a separate `TransactionLog` table for history.

**Pros:**
- Simple balance queries (single row read)
- Familiar CRUD pattern
- Lower storage for balance lookups

**Cons:**
- Balance and log can diverge (dual-write problem within the service)
- `UPDATE` on balance row creates lock contention
- No mathematical proof that balance is correct — only assertions
- Deleted or modified log entries destroy audit trail
- Harder to detect bugs or data corruption

### Option 2: Append-Only Ledger Entries with Cached Balance

All financial movements are recorded as immutable `LedgerEntry` rows. No `UPDATE` or `DELETE` is ever performed on this table. The balance is the sum of all entries for an account. A denormalized `AccountBalance` table caches the current balance with optimistic locking (`rowversion`).

**Pros:**
- Mathematically provable balance (sum of entries)
- Complete, immutable audit trail
- Error corrections via reversal entries preserve history
- No risk of lost evidence
- Cache can be rebuilt from entries at any time

**Cons:**
- Requires a separate cached balance table (denormalization)
- Reversal entries increase storage
- Balance cache must be kept in sync (within the same transaction)
- Full recalculation from entries is expensive for accounts with many transactions

## Decision Outcome

**Chosen option: Append-only ledger entries with cached balance.**

### Implementation Details

#### LedgerEntry Table

```
LedgerEntry
├── Id              : GUID (PK)
├── TransactionId   : GUID (groups debit+credit pair)
├── AccountId       : GUID (FK)
├── EntryType       : enum (Debit, Credit)
├── Amount          : decimal(19,4) (positive for credit, negative for debit)
├── Currency        : string (ISO 4217, e.g. "TRY")
├── Description     : string
├── CreatedAt       : datetime2
└── IdempotencyKey  : string (unique, for duplicate detection)
```

- **No UPDATE or DELETE** constraints enforced at the application layer and can be enforced via database triggers or permissions.
- Every transaction creates exactly 2 entries that sum to 0.

#### AccountBalance Table (Cache)

```
AccountBalance
├── AccountId       : GUID (PK)
├── Balance         : decimal(19,4)
├── Currency        : string
├── RowVersion      : rowversion (optimistic concurrency token)
└── LastUpdatedAt   : datetime2
```

- Updated in the **same database transaction** as the `LedgerEntry` insert.
- `RowVersion` enables optimistic concurrency: if two concurrent transactions try to update the same account's balance, one will get a `DbUpdateConcurrencyException` and retry.
- Can be fully rebuilt by running `SELECT AccountId, SUM(Amount) FROM LedgerEntry GROUP BY AccountId`.

#### Error Correction

Mistakes are corrected by posting **reversal entries**, not by modifying existing entries:

```
Original:  Account A -100 TRY (debit)   | Account B +100 TRY (credit)
Reversal:  Account A +100 TRY (credit)  | Account B -100 TRY (debit)
```

This preserves the full history: the original erroneous entries, the reversal, and (if needed) new corrected entries are all visible.

## Consequences

### Positive

- Complete, immutable audit trail — no data can be silently altered
- Balance is provably correct: `AccountBalance.Balance == SUM(LedgerEntry.Amount) WHERE AccountId = X`
- Reversal entries create a clear paper trail for corrections
- Cached balance enables O(1) balance queries
- Optimistic locking (`rowversion`) handles concurrent access without pessimistic locks
- Balance cache can be rebuilt from scratch if corruption is suspected

### Negative

- More storage than a mutable-balance approach (every correction doubles entries)
- Developers must remember to never issue UPDATE/DELETE on `LedgerEntry` (enforced by convention, optionally by DB permissions)
- Optimistic concurrency retries add latency under contention on hot accounts
- Two tables to maintain in sync (mitigated by same-transaction guarantee)
