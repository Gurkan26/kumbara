# ADR-0007: RabbitMQ vs. Kafka for Async Messaging

## Status

Accepted

## Context

Kumbara's microservices need an asynchronous messaging system for event-driven communication. The transactional outbox (see [ADR-0005](ADR-0005-outbox-pattern.md)) publishes events that need to be delivered to consumer services reliably. The saga orchestration (see [ADR-0004](ADR-0004-saga-orchestration.md)) relies on command/response messaging between the Transfer service and other services.

We need to choose a message broker that balances capability, simplicity, and alignment with our tech stack.

## Decision Drivers

- **MassTransit integration**: Our primary messaging abstraction is MassTransit; the broker must have excellent MassTransit support
- **Operational simplicity**: This is an educational project — the broker should be easy to set up, monitor, and debug
- **Message patterns**: We need pub/sub (events) and point-to-point (saga commands/responses)
- **Management UI**: Visual inspection of queues, exchanges, and messages aids debugging and learning
- **Scale requirements**: Low — this is a simulation, not a high-throughput production system

## Considered Options

### Option 1: RabbitMQ

Traditional message broker implementing AMQP. Mature, widely used, with a rich management UI and excellent MassTransit support.

**Pros:**
- First-class MassTransit transport — most examples and documentation use RabbitMQ
- Built-in Management UI (port 15672) for queue inspection, message rates, and manual operations
- Simple to run via Docker (`rabbitmq:management` image)
- Supports all required patterns: pub/sub (fanout/topic exchanges), point-to-point (direct queues), request/response
- Lightweight for small-to-medium workloads
- Dead-letter exchanges for failed message handling
- Well-understood operational model

**Cons:**
- No built-in replay from offset (messages are consumed and gone)
- Not designed for event sourcing / log compaction
- Performance degrades with very large queues (not an issue at our scale)
- No native exactly-once delivery (at-least-once with idempotent consumers)

### Option 2: Apache Kafka

Distributed event streaming platform. Designed for high-throughput, log-based message storage with offset replay.

**Pros:**
- Built-in message replay from any offset
- Log compaction for event sourcing patterns
- Extremely high throughput (millions of messages/sec)
- Messages persist for configurable retention periods
- Supports consumer groups for scaling

**Cons:**
- Significantly more complex to operate (Zookeeper/KRaft, partition management, replication)
- MassTransit Kafka transport is less mature than RabbitMQ transport
- Heavier Docker footprint (Kafka + ZooKeeper or KRaft)
- Overkill for our message volume (tens to hundreds of messages per transfer)
- Topic/partition model is more complex than exchange/queue for simple request/response
- Management UI requires additional tools (Kafka UI, Conduktor)
- Steeper learning curve for the messaging model itself

## Decision Outcome

**Chosen option: RabbitMQ**, because it aligns perfectly with our requirements and tech stack.

RabbitMQ provides everything Kumbara needs: reliable pub/sub for domain events, point-to-point queues for saga commands, dead-letter handling for failed messages, and a built-in management UI for debugging and learning. Its MassTransit integration is mature, well-documented, and covers all our patterns (saga state machines, outbox, inbox, consumers).

Kafka's strengths (high throughput, log replay, event sourcing) are not needed at Kumbara's scale. The added operational complexity of Kafka would detract from the project's educational focus on application-level patterns rather than infrastructure management.

## Consequences

### Positive

- Minimal setup: single Docker container (`rabbitmq:3-management`)
- MassTransit's RabbitMQ transport is battle-tested with comprehensive documentation
- Management UI at `localhost:15672` enables visual debugging of message flow
- Simple queue model aligns with saga command/response pattern
- Dead-letter exchanges catch and preserve failed messages for investigation
- Low resource footprint for local development

### Negative

- No built-in message replay — if a consumer needs to re-process old events, they must be re-published or sourced from the outbox table
- No log compaction — event sourcing patterns would require a different approach
- If the project later needs high-throughput event streaming, a migration to Kafka would be required
- Messages are transient by default (can be made persistent, which we do via MassTransit configuration)
