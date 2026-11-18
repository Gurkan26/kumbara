# Non-Functional Requirements (NFRs) & Verification Proofs

This document formalizes the non-functional requirements and quality attributes for the **Kumbara** educational fintech platform, along with concrete verification proofs, test results, and validation procedures.

---

## 1. Quality Attributes & Standards

| Quality Attribute | Target Metric | How Achieved | Verification Mechanism & Proof |
|---|---|---|---|
| **Financial Integrity & Correctness** | Zero balance drift; exact double-entry balance sum ($\sum \text{Debit} + \sum \text{Credit} = 0$) at all times. Zero lost funds. | Double-entry append-only ledger entries executed within a single database transaction. Strictly forbidden `UPDATE`/`DELETE` operations. Reversal entries for error correction. Domain `Money` value object backed by `decimal(19,4)`. | **Automated Invariant Checker** (`ILedgerInvariantChecker`):<br/>- `CheckDoubleEntryZeroSumAsync`: passes with 0 delta.<br/>- `CheckNoNegativeBalancesAsync`: passes (0 negative accounts).<br/>- `CheckTotalSystemFundsConservedAsync`: exact deposit matching.<br/>Unit tests in `InvariantCheckerTests.cs`. |
| **Data Consistency & Eventual Convergence** | Strong consistency within service boundaries; eventual consistency across microservice workflows (< 2 seconds under normal conditions). | ACID transactions with optimistic locking (`rowversion`) inside Ledger and Wallet. Transactional Outbox pattern paired with MassTransit Sagas across services. | **Transfer Saga Harness Tests** (`TransferSagaTests.cs`):<br/>- `TransferSaga_HappyPath_CompletesSuccessfully`: state converges to `Completed`.<br/>- `TransferSaga_CommitFails_TriggersCompensationAndReleasesHold`: hold released; balance restored. |
| **Idempotency** | 100% duplicate protection for mutating API calls and message consumers. Duplicate requests must produce zero extra mutations. | Mandatory `Idempotency-Key` HTTP header handled by ASP.NET Core middleware with 24-hour response caching in Redis/DB; MassTransit inbox deduplication (`InboxState`) for integration events. | **Idempotency Test** (`Idempotency_SameKey_ReturnsCachedResponse`):<br/>- Concurrently executing requests with identical keys yields cached HTTP 202 without duplicate saga execution or ledger balance debits. |
| **Availability & Fault Tolerance** | 99.9% uptime during operational testing; graceful degradation during isolated service outages. Zero data loss on transient broker failure. | Decentralized microservice architecture. If Risk service is temporarily offline, saga halts or queues requests rather than losing state. Transactional outbox keeps unpublished events safe on disk during RabbitMQ outages; exponential backoff retries with dead-letter queue (DLQ) routing. | **Chaos Recovery Simulation**:<br/>- RabbitMQ container stop & restart test: events buffered in `OutboxMessage` table and drained cleanly upon reconnection.<br/>- Service isolation test: killing Risk container keeps transfer in `AwaitingRisk` state without data corruption. |
| **Performance & Latency** | P95 API response time < 150ms for read queries; < 300ms for initiating asynchronous transfer sagas. | YARP lightweight reverse proxy routing; Redis caching for frequently accessed account balances; non-blocking asynchronous processing with EF Core and MassTransit. | **k6 Load Testing Suite** (`deploy/tests/load/k6-transfer.js`):<br/>- Threshold: `http_req_duration{p(95)} < 300ms`<br/>- Threshold: `http_req_failed < 0.01` (99% success under 200 concurrent VUs). |
| **Security & Authentication** | Zero unauthorized access to protected endpoints; strict token validation; tamper-proof credentials. | OAuth 2.0 / OpenID Connect authorization server powered by OpenIddict; ASP.NET Core JWT bearer authentication; BCrypt/Argon2 password hashing; principle of least privilege in SQL access. | **Security Tests** (`AuthenticationSecurityTests.cs`):<br/>- Rejection with 401 Unauthorized on absent or expired tokens.<br/>- Rejection with 403 Forbidden on missing roles/scopes. |
| **Auditability & Traceability** | 100% of financial modifications and saga state transitions traceable with timestamps and actor identities. | Immutable append-only `LedgerEntry` records; correlation IDs (`X-Correlation-Id`) propagated from Gateway through HTTP headers to MassTransit message headers and Serilog context. | **Seq Log Trace Inspection**:<br/>- Querying `@Properties.CorrelationId == '{guid}'` displays complete distributed chain from Gateway -> Transfer -> Risk -> Ledger. |
| **Maintainability & Modularity** | Clean Architecture separation; zero forbidden dependencies across domain boundaries. | Strict Clean Architecture project layers (Domain, Application, Infrastructure, Api); database-per-service isolation; Shared BuildingBlocks for common contracts. | **Architecture Fitness Tests** (`Kumbara.Architecture.Tests`):<br/>- NetArchTest rules enforcing Domain references no external layers, Controllers do not bypass Application layer. |
| **Observability** | Real-time visibility into microservice health, throughput, errors, and saga durations. | Serilog structured JSON logging directed to Seq; distributed correlation IDs; standard health check endpoints (`/health/live`, `/health/ready`). | **Health Probes**:<br/>- All 6 services implement ASP.NET Core Health Checks mapped to `/health/live` and `/health/ready`. |

---

## 2. Invariant Verification Proofs

The ledger enforces three strict mathematical invariants via `ILedgerInvariantChecker`:

### Invariant 1: Double-Entry Zero Sum
$$\sum_{i=1}^{N} \text{Debit}_i + \sum_{j=1}^{M} \text{Credit}_j = 0$$
- **Verification Rule**: For every transaction $T$, the sum of all associated `LedgerEntry.Amount` values must equal zero. Across the entire ledger, total debits must equal total credits.
- **Automated Check**: `CheckDoubleEntryZeroSumAsync()` executes `SELECT SUM(Amount) FROM LedgerEntries` and asserts the result is exactly `0.0000m`.

### Invariant 2: Non-Negative Available and Blocked Balances
$$\forall a \in \text{Accounts}: \text{AvailableBalance}(a) \ge 0 \quad \land \quad \text{BlockedBalance}(a) \ge 0$$
- **Verification Rule**: No account can have a negative available balance (overdrafts are prevented) or negative blocked balance (holds must be positive).
- **Automated Check**: `CheckNoNegativeBalancesAsync()` queries all `AccountBalance` records where `AvailableBalance < 0 OR BlockedBalance < 0` and asserts the count is zero.

### Invariant 3: Total System Funds Conservation
$$\sum_{a \in \text{Accounts}} (\text{AvailableBalance}_a + \text{BlockedBalance}_a) = \sum \text{Deposits} - \sum \text{Withdrawals}$$
- **Verification Rule**: Transfers between internal accounts conserve the total sum of money in the system. Money cannot appear or disappear.
- **Automated Check**: `CheckTotalSystemFundsConservedAsync(expectedFunds)` asserts that the sum of balances across all accounts exactly matches total net deposits.

---

## 3. k6 Performance Thresholds & Benchmark Suite

The platform performance is benchmarked using k6 test scenarios targeting the API Gateway:

```javascript
// k6 performance validation threshold configuration
export const options = {
  scenarios: {
    transfer_stress: {
      executor: 'ramping-vus',
      startVUs: 10,
      stages: [
        { duration: '30s', target: 50 },  // Warm-up
        { duration: '1m', target: 200 },   // Sustained load
        { duration: '30s', target: 0 },    // Ramp-down
      ],
      gracefulRampDown: '10s',
    },
  },
  thresholds: {
    // 95% of asynchronous transfer initiation requests must respond in under 300ms
    'http_req_duration{type:transfer_post}': ['p(95)<300'],
    // 95% of account balance query reads must respond in under 150ms
    'http_req_duration{type:balance_get}': ['p(95)<150'],
    // Maximum allowable HTTP error rate is under 1%
    'http_req_failed': ['rate<0.01'],
  },
};
```

---

## 4. Chaos Testing & Fault Recovery Procedures

### Scenario A: RabbitMQ Transient Outage
1. **Fault Injection**: RabbitMQ container is stopped while transfer requests are actively submitted.
2. **Behavior Observed**:
   - `TransferApi` continues accepting requests and commits saga state to SQL Server.
   - Domain events are written to the `OutboxMessage` table.
   - HTTP clients receive `202 Accepted`.
3. **Recovery Verification**:
   - RabbitMQ container is restarted.
   - MassTransit `BusOutboxDeliveryService` automatically detects reconnection, reads pending rows from `OutboxMessage`, and publishes them.
   - Downstream consumers (Risk, Ledger) process queued messages; transfers transition to `Completed`.
   - Zero messages lost; zero balance discrepancies.

### Scenario B: Risk Service Failure & Saga Suspension
1. **Fault Injection**: `Kumbara.Risk.Api` is terminated before processing `CheckTransferRiskCommand`.
2. **Behavior Observed**:
   - Command remains safely queued in RabbitMQ `check-transfer-risk` queue.
   - Transfer saga remains parked in `AwaitingRisk` state in SQL Server.
   - Ledger Service is **not** touched; no funds are held or committed.
3. **Recovery Verification**:
   - `Kumbara.Risk.Api` container is restarted.
   - Worker immediately consumes pending command from RabbitMQ queue, performs velocity check, and publishes `RiskApprovedEvent`.
   - Saga resumes cleanly from `AwaitingRisk` $\to$ `AwaitingHold` $\to$ `AwaitingCommit` $\to$ `Completed`.

### Scenario C: Optimistic Concurrency & Reversal Compensation
1. **Fault Injection**: High contention simulated by triggering multiple concurrent transfers on the same source account.
2. **Behavior Observed**:
   - EF Core detects conflicting `RowVersion` and raises `DbUpdateConcurrencyException`.
   - MassTransit retry policy performs exponential backoff and retry.
   - If retries are exhausted or commitment fails, saga dispatches `ReleaseLedgerHoldCommand`.
   - Blocked balance is released and returned to available balance.
   - Saga completes in `Failed` state with full compensation history recorded in the audit log.
