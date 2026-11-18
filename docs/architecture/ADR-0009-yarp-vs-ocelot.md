# ADR-0009: YARP vs. Ocelot for API Gateway

## Status

Accepted

## Context

Kumbara adopts a microservices architecture with multiple backend services (`Identity`, `Wallet`, `Ledger`, `Transfer`, `Risk`). Exposing individual microservice endpoints directly to clients (e.g., the Angular SPA or external API consumers) introduces coupling, security vulnerabilities, cross-origin resource sharing (CORS) complications, and inconsistent cross-cutting concerns (authentication, rate limiting, logging).

An API Gateway reverse proxy is required to act as the single entry point (`http://localhost:5000`) for client traffic, routing requests to the appropriate downstream microservices.

## Decision Drivers

- **.NET 8 Ecosystem Alignment**: First-class integration with ASP.NET Core middleware, dependency injection, and configuration models.
- **Performance & Throughput**: Minimal latency overhead and efficient HTTP routing.
- **Maintenance & Support**: Active support, documentation, and long-term roadmap from the platform vendor or community.
- **Extensibility**: Ease of integrating custom authentication token forwarding, header transformations, and health checks.

## Considered Options

### Option 1: YARP (Yet Another Reverse Proxy)
A reverse proxy toolkit developed by Microsoft, built directly on ASP.NET Core and .NET socket infrastructure.

- **Pros:**
  - Native Microsoft product, deeply integrated into ASP.NET Core 8 and Kestrel.
  - Exceptionally high throughput and low memory footprint compared to third-party abstractions.
  - Active development with rapid adoption across enterprise .NET applications.
  - Seamless configuration via `appsettings.json` or dynamic providers, fully composable with standard ASP.NET Core middleware.
- **Cons:**
  - Provides core reverse proxy primitives; higher-level plugins (e.g., pre-built aggregation) must be authored using standard ASP.NET Core middleware rather than out-of-the-box opinionated modules.

### Option 2: Ocelot
A popular open-source API gateway built specifically for .NET microservices.

- **Pros:**
  - Established community history with numerous turnkey features (aggregation, quality of service, tracing hooks).
  - Familiar configuration structure for existing .NET microservices developers.
- **Cons:**
  - Slower maintenance cycle and delayed compatibility updates for major new .NET releases.
  - Documented performance bottlenecks compared to YARP due to abstraction layers over ASP.NET Core pipelines.
  - Community momentum has largely shifted toward YARP as Microsoft's official reverse proxy solution.

## Decision Outcome

**Chosen option: YARP (Yet Another Reverse Proxy)**.

YARP provides native .NET 8 performance, ongoing official backing from Microsoft, and clean architectural alignment with ASP.NET Core middleware. Because Kumbara's gateway primarily requires route forwarding, JWT validation/forwarding, and health probe aggregation, YARP's lightweight, high-performance foundation is ideal.

## Consequences

### Positive
- High-performance routing directly on top of Kestrel with minimal latency overhead.
- Direct alignment with .NET 8 features and official Microsoft guidance.
- Simple, declarative routing definitions in `appsettings.json` for all microservices.
- Easy customization using standard ASP.NET Core middleware filters.

### Negative
- Less pre-packaged gateway-specific middleware than Ocelot; custom aggregation or transformation requires standard ASP.NET Core middleware implementation.
