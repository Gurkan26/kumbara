# Sequence Diagram: Transfer Saga — Failure & Compensation Scenarios

This document details the three primary failure paths in Kumbara's distributed money transfer flow, demonstrating how the MassTransit Saga State Machine handles rejections, resource shortages, and compensation rollback.

---

## Scenario A: Risk Rejected (Fail Early — Zero Ledger Impact)

When a transfer violates risk policies (e.g. amount exceeds 50,000 TRY, 24h rolling velocity exceeds 100,000 TRY, or high frequency activity detected), the Risk Service publishes `RiskRejectedEvent`. The saga transitions immediately to `Failed` without dispatching commands to the Ledger Service.

```mermaid
sequenceDiagram
    autonumber
    actor Client as 👤 Client
    participant Gateway as 🚪 YARP Gateway (:5000)
    participant TransferApi as 💸 Transfer API (:5004)
    participant Bus as 📨 RabbitMQ Bus
    participant Saga as 🔄 Transfer Saga State Machine
    participant Risk as 🛡️ Risk Service (:5005)
    participant Redis as ⚡ Redis (Velocity Store)
    participant Ledger as 📒 Ledger Service (:5003)

    Client->>Gateway: POST /api/transfers (Amount: 65,000 TRY)
    Gateway->>TransferApi: Forward Request
    TransferApi->>Bus: Publish TransferStartedEvent
    TransferApi-->>Client: 202 Accepted (Location: /api/transfers/{id})

    Bus->>Saga: Consume TransferStartedEvent
    Note over Saga: State: Initiated -> AwaitingRisk
    Saga->>Bus: Send CheckTransferRiskCommand
    Bus->>Risk: Consume CheckTransferRiskCommand

    Note over Risk: Evaluate SingleTransactionLimitRule:<br/>Amount (65,000 TRY) > Limit (50,000 TRY)
    Risk->>Bus: Publish RiskRejectedEvent<br/>(Reason: "SingleTransactionLimitExceeded")

    Bus->>Saga: Consume RiskRejectedEvent
    Note over Saga: State: AwaitingRisk -> Failed<br/>Ledger is NEVER contacted!<br/>No holds placed.
    Saga->>Bus: Publish TransferFailedEvent (Reason: "RiskRejected")

    Client->>Gateway: GET /api/transfers/{id}
    Gateway->>TransferApi: Forward Query
    TransferApi-->>Client: 200 OK (Status: Failed, Reason: "RiskRejected")
```

---

## Scenario B: Insufficient Funds (Hold Rejected — Clean Termination)

When risk evaluation passes, but the source account does not possess sufficient `AvailableBalance`, the Ledger Service rejects the hold request with `FundsHoldFailedEvent`. The saga transitions cleanly to `Failed` with no compensation required since no funds were held.

```mermaid
sequenceDiagram
    autonumber
    actor Client as 👤 Client
    participant TransferApi as 💸 Transfer API (:5004)
    participant Bus as 📨 RabbitMQ Bus
    participant Saga as 🔄 Transfer Saga State Machine
    participant Risk as 🛡️ Risk Service (:5005)
    participant Ledger as 📒 Ledger Service (:5003)
    participant DB as 🗄️ Ledger SQL DB

    TransferApi->>Bus: Publish TransferStartedEvent (Amount: 5,000 TRY)
    Bus->>Saga: Consume TransferStartedEvent
    Saga->>Bus: Send CheckTransferRiskCommand
    Risk->>Bus: Publish RiskApprovedEvent

    Bus->>Saga: Consume RiskApprovedEvent
    Note over Saga: State: AwaitingRisk -> AwaitingHold
    Saga->>Bus: Send HoldLedgerFundsCommand (Amount: 5,000 TRY)
    Bus->>Ledger: Consume HoldLedgerFundsCommand

    Note over Ledger: Query Source Account Balance
    Ledger->>DB: SELECT AvailableBalance FROM AccountBalance WHERE AccountId = @Src
    DB-->>Ledger: AvailableBalance = 1,200 TRY (< 5,000 TRY)
    Note over Ledger: Invariant Check: Insufficient funds!<br/>Abort hold creation.
    Ledger->>Bus: Publish FundsHoldFailedEvent<br/>(Reason: "InsufficientFunds")

    Bus->>Saga: Consume FundsHoldFailedEvent
    Note over Saga: State: AwaitingHold -> Failed<br/>No hold exists to release.
    Saga->>Bus: Publish TransferFailedEvent (Reason: "InsufficientFunds")

    Client->>TransferApi: GET /api/transfers/{id}
    TransferApi-->>Client: 200 OK (Status: Failed, Reason: "InsufficientFunds")
```

---

## Scenario C: Commit Failure & Compensation (Hold Released — Balance Restored)

When funds have been held successfully (`FundsHeldEvent`), but committing the transfer fails (e.g., destination account closed, optimistic locking failure, or database error), the saga enters compensation mode. It issues `ReleaseLedgerHoldCommand`, restoring the source account's available balance and transitioning to `Failed` (Compensated).

```mermaid
sequenceDiagram
    autonumber
    actor Client as 👤 Client
    participant TransferApi as 💸 Transfer API (:5004)
    participant Bus as 📨 RabbitMQ Bus
    participant Saga as 🔄 Transfer Saga State Machine
    participant Ledger as 📒 Ledger Service (:5003)
    participant DB as 🗄️ Ledger SQL DB

    Note over Saga: Funds held in previous step (HoldId: H-901)<br/>State: AwaitingCommit
    Saga->>Bus: Send CommitLedgerHoldCommand (TransferId, HoldId: H-901)
    Bus->>Ledger: Consume CommitLedgerHoldCommand

    Note over Ledger: Attempt DB Commit Transaction:<br/>Destination Account Closed / DB Invariant Error
    Ledger->>DB: Transaction Rollback
    Ledger->>Bus: Publish HoldCommitFailedEvent<br/>(Reason: "DestinationAccountClosed")

    Bus->>Saga: Consume HoldCommitFailedEvent
    Note over Saga: State: AwaitingCommit -> AwaitingCompensation<br/>COMPENSATION TRIGGERED
    Saga->>Bus: Send ReleaseLedgerHoldCommand (TransferId, HoldId: H-901)
    Bus->>Ledger: Consume ReleaseLedgerHoldCommand

    Note over Ledger: Begin Compensation Transaction
    Ledger->>DB: Release Hold:<br/>Source.BlockedBalance -= 5,000 TRY<br/>Source.AvailableBalance += 5,000 TRY<br/>Status: Released
    Ledger->>DB: Commit Compensation Transaction
    Ledger->>Bus: Publish HoldReleasedEvent (TransferId, HoldId: H-901)

    Bus->>Saga: Consume HoldReleasedEvent
    Note over Saga: State: AwaitingCompensation -> Failed<br/>Source balance fully restored!<br/>Zero money lost.
    Saga->>Bus: Publish TransferFailedEvent (Reason: "Compensated:CommitFailed")

    Client->>TransferApi: GET /api/transfers/{id}
    TransferApi-->>Client: 200 OK (Status: Failed, Reason: "Compensated")
```

## Summary of Failure Scenarios

| Failure Point | Triggering Event | Saga Transition | Compensation Action | Net Financial Impact |
|---|---|---|---|---|
| **Risk Validation** | `RiskRejectedEvent` | `AwaitingRisk` $\to$ `Failed` | None required. | Zero change in balances; no ledger records created. |
| **Balance Check** | `FundsHoldFailedEvent` | `AwaitingHold` $\to$ `Failed` | None required. | Zero change in balances; no hold placed. |
| **Commit Execution** | `HoldCommitFailedEvent` | `AwaitingCommit` $\to$ `AwaitingCompensation` $\to$ `Failed` | `ReleaseLedgerHoldCommand` unblocks reserved funds. | Temporary hold released; balances restored to pre-transfer state. |
