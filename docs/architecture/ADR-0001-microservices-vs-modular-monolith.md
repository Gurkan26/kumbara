# ADR-0001: Microservices vs. Modular Monolith

## Status

Accepted

## Context

Kumbara is an **educational fintech simulation** platform designed to demonstrate distributed systems patterns. The project simulates a digital wallet and money transfer system. We need to decide on the high-level architectural style.

Honestly, the functional requirements of this project (user management, wallet operations, ledger entries, transfers, basic risk checks) could be served by a single well-structured application. The expected user load is minimal — this is a learning tool, not a production fintech platform.

## Decision Drivers

- **Learning objectives**: The primary goal is to demonstrate and teach distributed systems patterns (saga, outbox, eventual consistency, service discovery, etc.)
- **Realistic simulation**: Students and developers should encounter real-world challenges of distributed architectures
- **Operational complexity tolerance**: Since this is educational, added complexity is a feature, not a bug
- **Team structure**: Contributors may work independently on separate bounded contexts
- **Deployment flexibility**: Each service can be deployed, scaled, and updated independently

## Considered Options

### Option 1: Modular Monolith

A single deployable unit with well-defined internal modules (Identity, Wallet, Ledger, Transfer, Risk) communicating via in-process method calls or an internal event bus.

**Pros:**
- Simpler to develop, debug, and deploy
- No network overhead between modules
- Simpler transactions (single database, ACID guarantees)
- Lower operational cost (one process, one database)
- Easier to refactor module boundaries

**Cons:**
- Does not expose learners to distributed systems challenges
- Cannot independently scale modules
- Shared database creates coupling risk over time
- Less realistic for fintech industry patterns

### Option 2: Microservices

Each bounded context (Identity, Wallet, Ledger, Transfer, Risk) is a separately deployable service with its own database, communicating via async messaging and synchronous APIs.

**Pros:**
- Demonstrates real-world distributed patterns
- Independent deployment and scaling per service
- Strong data isolation (database-per-service)
- Technology heterogeneity possible per service
- Forces explicit API contracts and event-driven design

**Cons:**
- Significant operational complexity
- Network latency and partial failure handling
- Requires saga/outbox patterns for data consistency
- Harder to debug across service boundaries
- Over-engineered for the actual scale

## Decision Outcome

**Chosen option: Microservices**, because the primary purpose of Kumbara is education. A modular monolith would be the pragmatically correct choice for the actual scale and requirements. However, adopting microservices forces the implementation of patterns that are the core learning objectives:

- Saga orchestration for distributed transactions
- Transactional outbox for reliable event publishing
- Idempotency for at-least-once message delivery
- Database-per-service for data isolation
- API gateway for unified entry point

## Consequences

### Positive

- Rich learning environment for distributed systems patterns
- Each service can serve as a standalone teaching example
- Contributors can work on services independently
- Realistic simulation of fintech architecture challenges
- Forces good practices: explicit contracts, event-driven communication, fault tolerance

### Negative

- Higher complexity to set up and run locally (Docker Compose required)
- Operational overhead: multiple databases, message broker, service discovery
- Debugging requires distributed tracing (Seq/structured logging)
- Eventual consistency introduces complexity in read models
- More infrastructure code (outbox, idempotency, saga) compared to business logic
- Risk of "distributed monolith" if service boundaries are poorly chosen
