# Architecture Decision Records (ADRs)

This directory contains records of significant architectural decisions made for the **Kumbara** project. We adhere to the [MADR (Markdown Any Architecture Decision Record)](https://adr.github.io/madr/) format.

## Overview

| ADR | Title | Status | Date | Summary |
|---|---|---|---|---|
| [ADR-0001](ADR-0001-microservices-vs-modular-monolith.md) | Microservices vs. Modular Monolith | Accepted | 2026-11 | Adopted microservices over modular monolith for educational demonstrations of distributed patterns. |
| [ADR-0002](ADR-0002-ledger-strong-consistency.md) | Ledger Strong Consistency Model | Accepted | 2026-11 | Strong consistency within Ledger (single DB transaction), eventual consistency across services via saga + outbox. |
| [ADR-0003](ADR-0003-double-entry-append-only.md) | Double-Entry Append-Only Ledger | Accepted | 2026-11 | Append-only ledger entries summing to zero. Error correction via reversal entries; cached balances with `rowversion`. |
| [ADR-0004](ADR-0004-saga-orchestration.md) | Saga Orchestration for Transfer Flow | Accepted | 2026-11 | Centralized orchestration using MassTransit State Machine over choreography for clear state tracking and compensation. |
| [ADR-0005](ADR-0005-outbox-pattern.md) | Transactional Outbox Pattern | Accepted | 2026-11 | Solves the dual-write problem by persisting outbox messages in the same DB transaction as domain state changes. |
| [ADR-0006](ADR-0006-idempotency-strategy.md) | Idempotency Strategy | Accepted | 2026-11 | Mandates `Idempotency-Key` header for mutating HTTP endpoints with DB caching, plus message deduplication via inbox. |
| [ADR-0007](ADR-0007-rabbitmq-vs-kafka.md) | RabbitMQ vs. Kafka for Async Messaging | Accepted | 2026-11 | Selected RabbitMQ for mature MassTransit integration, ease of setup, and built-in management UI. |
| [ADR-0008](ADR-0008-database-per-service.md) | Database-per-Service | Accepted | 2026-11 | Strict data isolation where each microservice owns its schema and database; cross-service data accessed via events/APIs. |
| [ADR-0009](ADR-0009-yarp-vs-ocelot.md) | YARP vs. Ocelot for API Gateway | Accepted | 2026-11 | Selected Microsoft's YARP for native .NET 8 performance, high throughput, and active platform backing. |
| [ADR-0010](ADR-0010-money-representation.md) | Money Representation and Precision | Accepted | 2026-11 | Standardized on `decimal(19,4)` and an immutable `Money` Value Object with ISO 4217 currency codes; banned floats. |
