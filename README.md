# Catalog.API

## Authentication

Catalog API uses ASP.NET Core Identity + JWT bearer authentication, implemented as the reusable
`Identity.Authentication` class library (`Identity.Authentication/`). Any future ASP.NET Core Web
API can reference the library, call `AddIdentityAuthentication(configuration)` and
`AddJwtAuthentication(configuration)`, and get user registration/login, password hashing, and JWT
issuance/validation with a `Jwt` configuration section:

```json
"Jwt": {
  "Key": "...",
  "Issuer": "...",
  "Audience": "...",
  "ExpirationMinutes": 60
}
```

Endpoints exposed by the library and mounted on Catalog API:

- `POST /api/auth/register` — creates a user (`{ email, password }`)
- `POST /api/auth/login` — returns `{ accessToken, expiresAtUtc }` on success

Identity tables (`AspNetUsers`, `AspNetRoles`, etc.) live in the same SQL Server database as the
catalog domain data, tracked under a separate EF Core migrations history table
(`__EFMigrationsHistory_Identity`) to avoid colliding with `CatalogContext`'s own migrations.

## Event-Driven Architecture

Catalog API publishes domain events (item created/updated/deleted) reliably, and `Catalog.Worker`
(a separate, independently-scalable process) consumes them. The reusable core is the `EventBus`
class library (`EventBus/`) — no dependency on Catalog.Domain/Infrastructure, so any future project
can reference it the same way it references `Identity.Authentication`.

**How it works:**
- **Publishing = Transactional Outbox.** `IOutbox.EnqueueAsync(...)` writes the event to an
  `OutboxMessages` table (`eventbus` schema) in the *same DB transaction* as the business change —
  a crash between the DB commit and the RabbitMQ publish can never silently lose the event.
  A background `OutboxProcessorHostedService` claims unpublished rows with an atomic, lease-based
  SQL `UPDATE ... OUTPUT` (safe across multiple `catalog_api` replicas — no double-publish) and
  ships them to RabbitMQ with publisher confirms enabled.
- **Consuming = competing consumers with retry + DLQ.** `Catalog.Worker` binds a durable queue per
  event type. A failed handler is retried via backoff-tier queues (5s → 30s → 2m) using RabbitMQ's
  native TTL + dead-letter-exchange plumbing (no delay plugin needed); after 5 attempts the message
  lands in a dead-letter queue instead of blocking the main queue.
- **Idempotency.** Because RabbitMQ redelivery is at-least-once, `Catalog.Worker` records each
  handled event ID in a `ProcessedEvents` table before invoking the handler — a duplicate delivery
  is detected via a primary-key violation and skipped.

**Publishing a new event** (from any service that already writes to a DbContext):
```csharp
// 1. Define the event in Catalog.Contracts/Events/
public record OrderPlacedIntegrationEvent : IntegrationEvent { public Guid OrderId { get; init; } }

// 2. Inject IOutbox where the business change happens, enqueue before SaveChangesAsync
await _outbox.EnqueueAsync(new OrderPlacedIntegrationEvent { OrderId = order.Id }, cancellationToken);
await _context.SaveChangesAsync(cancellationToken);
```
Delivery is then guaranteed by the outbox dispatcher — no other wiring required.

**Consuming an event** (in `Catalog.Worker`):
```csharp
// 1. Add a handler in Catalog.Worker/Handlers/
public class OrderPlacedEventHandler : IIntegrationEventHandler<OrderPlacedIntegrationEvent> { ... }

// 2. Register it in Program.cs
builder.Services.AddEventConsumer<WorkerDbContext>(config, subs => subs
    .Subscribe<OrderPlacedIntegrationEvent, OrderPlacedEventHandler>());
```

**Scaling consumers** horizontally (competing consumers, RabbitMQ round-robins deliveries across
replicas):
```powershell
docker compose up -d --scale catalog_worker=3
```

See the plan doc for the full design rationale (outbox claim SQL, retry-tier topology, etc.). Out of
scope for this pass — deferred as a documented roadmap: hardening the Redis cache-aside logic in
`ItemController` (a natural consumer of `ItemUpdatedIntegrationEvent`/`ItemDeletedIntegrationEvent`),
DB indexing/schema review, and Kubernetes/production deployment topology.
