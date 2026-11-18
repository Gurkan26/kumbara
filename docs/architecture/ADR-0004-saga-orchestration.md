# ADR-0004: Saga Orchestration for Transfer Flow

## Status

Accepted

## Context

A money transfer in Kumbara involves multiple services:

1. **Risk Service** — Assess the transaction for fraud/policy violations
2. **Ledger Service** — Place a hold (debit source account), then commit (credit destination account)
3. **Transfer Service** — Track the transfer lifecycle and coordinate the above

If any step fails, previous steps must be compensated (e.g., release the hold). We need a pattern to coordinate this multi-step, cross-service workflow reliably.

## Decision Drivers

- **Reliability**: Partial failures must not leave money in limbo (held but never committed or released)
- **Visibility**: The current state of a transfer must be queryable at any time
- **Compensation**: Each step must have a well-defined rollback action
- **Debuggability**: Complex flows must be easy to reason about and troubleshoot
- **Tooling**: The pattern should be well-supported by our messaging framework (MassTransit)

## Considered Options

### Option 1: Choreography (Event-Driven)

Each service reacts to events from other services. No central coordinator. For example:
- Transfer service publishes `TransferRequested`
- Risk service listens, assesses, publishes `RiskAssessmentCompleted`
- Ledger service listens, places hold, publishes `HoldPlaced`
- And so on…

**Pros:**
- Loose coupling — no service knows about the others
- Simple to add new steps (just subscribe to events)
- No single point of failure for coordination

**Cons:**
- Flow logic is scattered across services — hard to understand the full picture
- Compensation is complex — each service must know what to undo and when
- No single place to query "what state is this transfer in?"
- Circular dependencies can emerge
- Debugging requires correlating events across multiple services
- Adding conditional logic (e.g., skip risk check for small amounts) is awkward

### Option 2: Orchestration (Saga State Machine)

A central saga in the Transfer service coordinates the flow. It sends commands to other services and reacts to their responses, maintaining a state machine that tracks the transfer lifecycle.

**Pros:**
- Flow logic is in one place — easy to read, reason about, and modify
- Clear state machine: `Initiated → RiskChecking → Holding → Committing → Completed` (or `→ Failed`)
- Compensation logic is co-located with the step that triggered it
- Transfer state is always queryable
- Well-supported by MassTransit's `MassTransitStateMachine`

**Cons:**
- Transfer service becomes a coordinator — a logical single point of failure (mitigated by persistent state)
- Tighter coupling: the saga knows about Risk and Ledger service contracts
- More complex Transfer service code

## Decision Outcome

**Chosen option: Orchestration via MassTransit Saga State Machine.**

### Transfer Saga Flow

```
                    ┌─────────────┐
                    │  Initiated  │
                    └──────┬──────┘
                           │ SendRiskAssessmentCommand
                           ▼
                    ┌──────────────┐
              ┌─────│ RiskChecking │
              │     └──────┬───────┘
              │            │ RiskApproved
              │            ▼
              │     ┌────────────┐
              │  ┌──│  Holding   │
              │  │  └─────┬──────┘
              │  │        │ HoldPlaced
              │  │        ▼
              │  │  ┌─────────────┐
              │  │  │ Committing  │──── CommitFailed ──┐
              │  │  └──────┬──────┘                    │
              │  │         │ Committed                 │
              │  │         ▼                           ▼
              │  │  ┌─────────────┐            ┌──────────────┐
              │  │  │  Completed  │            │ Compensating │
              │  │  └─────────────┘            └──────┬───────┘
              │  │                                    │
              │  └── HoldFailed ──────────────────────┤
              │                                       │
              └──── RiskRejected ─────────────────────┤
                                                      ▼
                                               ┌────────────┐
                                               │   Failed   │
                                               └────────────┘
```

### Key Design Decisions

- **Persistent state**: Saga state is stored in the Transfer database (EF Core). If the Transfer service restarts, in-flight sagas resume from their last persisted state.
- **Compensation**: If `HoldPlaced` but `Commit` fails, the saga sends a `ReleaseHoldCommand` to the Ledger service. If `RiskRejected`, no compensation is needed (no hold was placed).
- **Timeouts**: Each step has a timeout. If Risk or Ledger does not respond within the timeout, the saga moves to `Failed` and compensates.
- **Idempotency**: The saga uses a correlation ID (the `TransferId`) to ensure duplicate events do not create duplicate sagas.

## Consequences

### Positive

- Clear, centralized flow logic — new developers can read the state machine to understand the transfer lifecycle
- Full state visibility — query the saga's current state at any time
- Compensation is explicit and co-located with the step definition
- MassTransit handles message correlation, retries, and state persistence
- Timeouts prevent transfers from being stuck in intermediate states forever

### Negative

- Transfer service is a logical coordinator — if its database is down, no transfers can progress (mitigated by standard HA practices)
- Saga knows about Risk and Ledger contracts (commands and events), creating coupling at the contract level
- MassTransit saga state machine has a learning curve
- State machine code can become complex as more steps or conditions are added
