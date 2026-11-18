# ADR-0010: Money Representation and Precision

## Status

Accepted

## Context

Financial transactions in Kumbara require exact precision and strict currency handling. Binary floating-point representations (such as `float` or `double` in C# and SQL) are inherently imprecise due to base-2 representation of fractional values, resulting in rounding errors that violate financial integrity. Furthermore, amounts cannot exist in isolation without an explicit currency context (e.g., 100 TRY != 100 USD).

We must establish a standardized domain model and persistence strategy for all monetary amounts across the system.

## Decision Drivers

- **Zero Tolerance for Precision Loss**: Rounding artifacts or floating-point drift can result in lost or phantom funds.
- **Explicit Currency Association**: Every amount must be bound to an ISO 4217 currency code to prevent accidental cross-currency operations.
- **Database Consistency & Standard Types**: Schema definition must be consistent across SQL Server tables (`LedgerEntry`, `AccountBalance`, `Transfer`).
- **Domain Modeling Best Practices**: Monetary concepts should be modeled as immutable Value Objects in DDD.

## Considered Options

### Option 1: Floating-Point Types (`float` / `double`)
Store amounts as standard IEEE 754 floating-point numbers.

- **Pros:**
  - Hardware-accelerated arithmetic; very fast execution.
  - Native standard type in all languages and databases.
- **Cons:**
  - Inherent precision loss and rounding errors (e.g., `0.1 + 0.2 != 0.3`).
  - Completely unacceptable for accounting and ledger systems.

### Option 2: Minor Units (Cents/Kuruş) as Integers (`long` / `bigint`)
Store amounts as integer representations of the currency's smallest sub-unit (e.g., 100.50 TRY stored as `10050` kuruş).

- **Pros:**
  - Integer operations are fast and mathematically exact.
  - Standard practice in payment APIs like Stripe.
- **Cons:**
  - Requires currency-specific multiplier logic (e.g., JPY has 0 decimals, BHD has 3 decimals, TRY/USD has 2).
  - Complicates reporting, ad-hoc SQL querying, and database readability without formatting helpers.

### Option 3: Fixed-Point Decimal and Money Value Object (`decimal(19,4)` + ISO 4217)
Model money in the domain as an immutable `Money` Value Object wrapping a `decimal` amount and an ISO 4217 currency string. Persist in SQL Server as `decimal(19,4)` (or EF Core value conversion/complex property).

- **Pros:**
  - Exact decimal representation in base-10; completely eliminates binary floating-point drift.
  - 4 decimal places support micro-charges, interest calculations, and standard sub-unit rounding without truncation.
  - Highly readable in database schemas, logs, and reporting tools.
  - Value Object enforces invariants (e.g., cannot add TRY to EUR without conversion).
- **Cons:**
  - Slightly slower arithmetic than primitive integer operations in CPU benchmarks (negligible for web and transaction workloads).
  - Requires consistent EF Core mapping configuration across services.

## Decision Outcome

**Chosen option: Fixed-Point Decimal and Money Value Object (`decimal(19,4)` + ISO 4217)**.

### Implementation Details

1. **Domain Model**:
   - Introduce an immutable `Money` value object containing:
     - `Amount`: `decimal`
     - `Currency`: `string` (validated against ISO 4217 codes such as `TRY`, `USD`, `EUR`)
   - Enforce domain rules (e.g., throwing exceptions when attempting to add or subtract `Money` instances with mismatched currencies).
2. **Database Persistence**:
   - Column definition in SQL Server: `decimal(19,4)`.
   - 19 total digits allows up to 15 integer digits (trillions) with 4 decimal places of fractional precision.
3. **Floating Point Ban**:
   - `float` and `double` are strictly prohibited in domain entities, DTOs, contracts, and database columns representing financial values.

## Consequences

### Positive
- Prevents rounding drift and mathematical discrepancies in financial calculations.
- Prevents cross-currency calculation bugs at compile-time and runtime via domain type safety.
- Clear and audit-friendly values stored directly in SQL Server tables.
- Provides 4 decimal places of sub-unit precision for financial intermediate steps.

### Negative
- Minor CPU overhead for `decimal` calculations compared to 64-bit integer arithmetic (insignificant for transactional throughput).
- Serialization/deserialization across DTO contracts requires standard serialization rules or flattened properties (`Amount`, `Currency`).
