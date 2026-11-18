# Sequence Diagram: Transfer Saga — Happy Path

This document illustrates the end-to-end execution flow of a successful money transfer in the **Kumbara** platform, coordinated via the MassTransit Saga State Machine (`TransferStateMachine`).

## Architectural Flow Overview

1. **Client Submission**: Client sends an asynchronous transfer request to the API Gateway with an `Idempotency-Key`.
2. **Gateway Dispatch**: Gateway proxies to Transfer Service; Transfer initiates saga state and immediately returns `202 Accepted`.
3. **Risk Evaluation**: Saga requests risk verification from the Risk Service; Redis velocity counters are evaluated and approved.
4. **Ledger Hold**: Saga dispatches fund hold command to Ledger Service; funds are reserved on source account.
5. **Ledger Commit**: Saga dispatches commit command; Ledger creates double-entry debit/credit entries and updates cached balances.
6. **Completion**: Saga reaches final `Completed` state and publishes `TransferCompletedEvent`.

---

## Detailed Sequence Diagram

```mermaid
sequenceDiagram
    autonumber
    actor Client as 👤 Client (SPA / API Consumer)
    participant Gateway as 🚪 YARP Gateway (:5000)
    participant TransferApi as 💸 Transfer API (:5004)
    participant Bus as 📨 RabbitMQ Bus
    participant Saga as 🔄 Transfer Saga State Machine
    participant Risk as 🛡️ Risk Service (:5005)
    participant Redis as ⚡ Redis (Velocity Store)
    participant Ledger as 📒 Ledger Service (:5003)
    participant DB as 🗄️ SQL Server

    %% Step 1: Initial Request
    Client->>Gateway: POST /api/transfers<br/>Headers: Idempotency-Key, Authorization
    Note over Gateway: Validates JWT Bearer & Rate Limits<br/>Injects X-Correlation-ID
    Gateway->>TransferApi: Forward Request with Correlation ID

    %% Step 2: Idempotency & Acceptance
    Note over TransferApi: Check Idempotency Key in DB/Cache
    TransferApi->>DB: Insert Transfer (Status: Initiated)
    TransferApi->>Bus: Publish TransferStartedEvent (TransferId, Source, Dest, Amount)
    TransferApi-->>Gateway: 202 Accepted (Location: /api/transfers/{id})
    Gateway-->>Client: 202 Accepted (Location: /api/transfers/{id})

    %% Step 3: Saga Initialization & Risk Assessment
    Bus->>Saga: Consume TransferStartedEvent
    Note over Saga: State: Initiated -> AwaitingRisk<br/>Persist Saga State to DB
    Saga->>Bus: Send CheckTransferRiskCommand
    Bus->>Risk: Consume CheckTransferRiskCommand

    Note over Risk: Evaluate IRiskRules:<br/>1. SingleTransactionLimit (<= 50,000 TRY)<br/>2. DailyVelocity (<= 100,000 TRY / 24h)<br/>3. FrequencyLimit (<= 5 / 5min)
    Risk->>Redis: Query & Increment Velocity Counters
    Redis-->>Risk: Velocity Ok
    Note over Risk: Risk Result: Approved
    Risk->>Bus: Publish RiskApprovedEvent (TransferId)

    %% Step 4: Ledger Hold
    Bus->>Saga: Consume RiskApprovedEvent
    Note over Saga: State: AwaitingRisk -> AwaitingHold<br/>Persist Saga State
    Saga->>Bus: Send HoldLedgerFundsCommand (TransferId, AccountId, Amount)
    Bus->>Ledger: Consume HoldLedgerFundsCommand

    Note over Ledger: Begin DB Transaction<br/>Verify Source AvailableBalance >= Amount
    Ledger->>DB: Lock Account & Reserve Hold:<br/>AvailableBalance -= Amount<br/>BlockedBalance += Amount
    Ledger->>DB: Commit DB Transaction
    Ledger->>Bus: Publish FundsHeldEvent (TransferId, HoldId)

    %% Step 5: Ledger Commit
    Bus->>Saga: Consume FundsHeldEvent
    Note over Saga: State: AwaitingHold -> AwaitingCommit<br/>Persist Saga State
    Saga->>Bus: Send CommitLedgerHoldCommand (TransferId, HoldId)
    Bus->>Ledger: Consume CommitLedgerHoldCommand

    Note over Ledger: Begin DB Transaction<br/>Double-Entry Bookkeeping
    Ledger->>DB: Insert LedgerEntry (Debit: -Amount, SourceAccount)
    Ledger->>DB: Insert LedgerEntry (Credit: +Amount, DestAccount)<br/>SUM(Entries) == 0
    Ledger->>DB: Release Hold & Update Final Balances:<br/>Source.BlockedBalance -= Amount<br/>Dest.AvailableBalance += Amount<br/>Check RowVersion (Optimistic Locking)
    Ledger->>DB: Commit DB Transaction
    Ledger->>Bus: Publish HoldCommittedEvent (TransferId)

    %% Step 6: Completion
    Bus->>Saga: Consume HoldCommittedEvent
    Note over Saga: State: AwaitingCommit -> Completed<br/>Finalize Saga State
    Saga->>DB: Update Transfer Status = Completed
    Saga->>Bus: Publish TransferCompletedEvent (TransferId)

    %% Step 7: Client Query / Polling
    Client->>Gateway: GET /api/transfers/{id}
    Gateway->>TransferApi: Forward Query
    TransferApi->>DB: Query Transfer State
    DB-->>TransferApi: Transfer Record (Status: Completed)
    TransferApi-->>Gateway: 200 OK (Status: Completed)
    Gateway-->>Client: 200 OK (Status: Completed)
```

## Key State Transitions

| Phase | State Machine State | Trigger Message | Action Taken |
|---|---|---|---|
| **Initiation** | `Initial` $\to$ `AwaitingRisk` | `TransferStartedEvent` | Persists saga instance; dispatches `CheckTransferRiskCommand`. |
| **Risk Check** | `AwaitingRisk` $\to$ `AwaitingHold` | `RiskApprovedEvent` | Updates saga; dispatches `HoldLedgerFundsCommand`. |
| **Fund Hold** | `AwaitingHold` $\to$ `AwaitingCommit` | `FundsHeldEvent` | Records `HoldId`; dispatches `CommitLedgerHoldCommand`. |
| **Commit** | `AwaitingCommit` $\to$ `Completed` | `HoldCommittedEvent` | Updates transfer entity to `Completed`; publishes `TransferCompletedEvent`. |
