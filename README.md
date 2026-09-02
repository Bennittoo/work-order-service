# Work Order Service

A backend service for managing work orders on network infrastructure rollouts, and for ingesting
progress events from external systems asynchronously.

.NET 8, ASP.NET Core Minimal APIs, EF Core, SQL Server, xUnit.

Design decisions and trade-offs are in [SOLUTION.md](SOLUTION.md). How AI tooling was used is in
[AI_USAGE.md](AI_USAGE.md).

## Running it locally

### 1. Start SQL Server

```bash
docker compose up -d
```

This starts SQL Server 2022 on `localhost:14330` with the credentials the default connection string
expects. Port 14330 rather than 1433 is deliberate: if you already have SQL Server installed it is
listening on 1433, Windows allows both to bind it, and your local instance wins the connection while
the container sits there unreachable. Wait for the container to report healthy:

```bash
docker compose ps
```

To use an instance you already have instead, override the connection string:

```bash
export ConnectionStrings__WorkOrders="Server=.\\SQLEXPRESS;Database=WorkOrderService;Trusted_Connection=True;TrustServerCertificate=True"
```

On PowerShell:

```powershell
$env:ConnectionStrings__WorkOrders = "Server=.\SQLEXPRESS;Database=WorkOrderService;Trusted_Connection=True;TrustServerCertificate=True"
```

### 2. Create the schema

The application does not migrate on startup, deliberately (see SOLUTION.md). Apply the migration
yourself:

```bash
dotnet tool restore
```

```bash
dotnet ef database update --project src/WorkOrderService.Api --startup-project src/WorkOrderService.Api
```

### 3. Run

```bash
dotnet run --project src/WorkOrderService.Api
```

The service listens on `http://localhost:5045`. A browser opening the root is redirected to Swagger
UI at `/swagger`; the OpenAPI document is at `/swagger/v1/swagger.json`.

### 4. Run the tests

```bash
dotnet test
```

No database required: the domain tests need none, and the integration tests bring their own SQLite
file. See [Testing](#testing) for what that does and does not cover.

## Configuration

| Setting | Default | Purpose |
| --- | --- | --- |
| `ConnectionStrings:WorkOrders` | containerised SQL Server | Database connection |
| `ApiKey:Value` | `local-development-key-not-a-secret` | Key required on write endpoints |
| `ApiKey:HeaderName` | `X-Api-Key` | Header carrying the key |
| `ProgressEvents:QueueCapacity` | `1000` | Bounded queue size for accepted events |
| `ProgressEvents:MaxProcessingAttempts` | `3` | Reprocessing attempts after a concurrency conflict |

The committed API key is a local development placeholder, not a secret, and the service refuses to
start if no key is configured at all. Override it per environment:

```bash
export ApiKey__Value="something-else"
```

## API

Writes (`POST`, `PUT`) require `X-Api-Key`. Reads do not.

| Method | Route | Purpose |
| --- | --- | --- |
| `POST` | `/api/work-orders` | Create a work order |
| `GET` | `/api/work-orders/{id}` | Fetch one, with its full status history |
| `GET` | `/api/work-orders?status=&page=` | List, optional status filter, fixed page size of 25 |
| `PUT` | `/api/work-orders/{id}/status` | Change status |
| `POST` | `/api/progress-events` | Submit a progress event for background processing |
| `GET` | `/health` | Liveness |

Errors use `application/problem+json`. Enums are strings on the wire in both directions, and the
status filter is matched case-insensitively.

### Create a work order

```bash
curl -X POST http://localhost:5045/api/work-orders -H "Content-Type: application/json" -H "X-Api-Key: local-development-key-not-a-secret" -d '{"externalId":"EXT-1001","siteCode":"JHB-042","description":"Install equipment at JHB-042"}'
```

`201 Created`, with `Location: /api/work-orders/{id}`:

```json
{
  "id": "f1703e5c-272e-447c-90b5-b74fb320b8b9",
  "externalId": "EXT-1001",
  "siteCode": "JHB-042",
  "description": "Install equipment at JHB-042",
  "status": "Pending",
  "createdAt": "2026-09-01T10:34:00.0832062+00:00",
  "updatedAt": "2026-09-01T10:34:00.0832062+00:00",
  "statusHistory": [
    {
      "fromStatus": null,
      "toStatus": "Pending",
      "occurredAt": "2026-09-01T10:34:00.0832062+00:00",
      "recordedAt": "2026-09-01T10:34:00.0832062+00:00",
      "source": "Creation",
      "details": null,
      "eventId": null
    }
  ]
}
```

A second work order with the same `externalId` returns `409 Conflict`.

### Change status

```bash
curl -X PUT http://localhost:5045/api/work-orders/{id}/status -H "Content-Type: application/json" -H "X-Api-Key: local-development-key-not-a-secret" -d '{"status":"InProgress","details":"Crew on site"}'
```

`200 OK` with the full work order. Setting the status it already holds also returns `200`, and adds
no history entry. An illegal transition returns `409 Conflict`:

```json
{
  "title": "Status change not allowed",
  "status": 409,
  "detail": "Cannot move from Pending to Completed. Allowed from Pending: InProgress, Cancelled."
}
```

### List

```bash
curl "http://localhost:5045/api/work-orders?status=InProgress&page=1"
```

```json
{
  "items": [
    {
      "id": "f1703e5c-272e-447c-90b5-b74fb320b8b9",
      "externalId": "EXT-1001",
      "siteCode": "JHB-042",
      "description": "Install equipment at JHB-042",
      "status": "InProgress",
      "createdAt": "2026-09-01T10:34:00.0832062+00:00",
      "updatedAt": "2026-09-01T10:34:01.6939943+00:00"
    }
  ],
  "page": 1,
  "pageSize": 25,
  "totalCount": 1,
  "totalPages": 1
}
```

### Submit a progress event

```bash
curl -X POST http://localhost:5045/api/progress-events -H "Content-Type: application/json" -H "X-Api-Key: local-development-key-not-a-secret" -d '{"eventId":"c32892fd-1d8b-4eb8-ab31-aea5d9077f2b","workOrderExternalId":"EXT-1001","newStatus":"InProgress","occurredAt":"2026-09-01T09:00:00Z","details":"Crew dispatched"}'
```

`202 Accepted`:

```json
{ "eventId": "c32892fd-1d8b-4eb8-ab31-aea5d9077f2b", "status": "Accepted" }
```

The 202 means the event was taken, not that it was valid. Whether the work order exists and whether
the transition is legal are decided by the background processor, and the outcome is observed by
reading the work order. Submitting the same `eventId` again is safe: it produces no second status
change and no second history entry.

If the queue is full, the endpoint returns `503 Service Unavailable` with a `Retry-After` header
rather than dropping an event it has already acknowledged.

## Project layout

```
src/
  WorkOrderService.Domain         Entities and the status lifecycle. No EF Core reference.
  WorkOrderService.Api            Minimal API endpoints, EF Core, migrations, background processing.
tests/
  WorkOrderService.Domain.Tests   Business rules, no database.
  WorkOrderService.Api.Tests      The running application, end to end.
```

The domain is a separate project so the lifecycle rules can be tested without EF Core. There is no
separate infrastructure project: there is one persistence concern and no second adapter, so the
extra layer would have been ceremony.

## Testing

`dotnet test` runs 56 tests: 34 domain tests, and 22 integration tests driving the real application
through `WebApplicationFactory`.

The integration tests run against **SQLite**, in a temporary file created per test class and deleted
afterwards. Four consequences a reviewer should know about rather than discover:

- **In-memory SQLite is deliberately not used.** An in-memory database lives inside a single open
  connection, and this service has two concurrent writers by design: the request path and the
  background processor. Sharing one connection between them produces `database is locked` instead of
  the behaviour under test. A file gives each context its own connection, real lock handling through
  the busy timeout, and WAL so a reader is not blocked by the worker mid-write.
- **Concurrency conflicts are not covered.** SQLite has no `rowversion`, so under that provider the
  concurrency token is configured as never generated. Optimistic concurrency is exercised against
  SQL Server only, by hand.
- **The migration is not covered.** The test schema is built with `EnsureCreated` from the model,
  because the migration contains SQL Server specifics (`rowversion`, clustered index placement).
  These tests prove the model is right, not that the migration is. The migration was applied to a
  real SQL Server separately.
- **`DateTimeOffset` is stored differently.** SQLite cannot order by one, because its text encoding
  embeds the offset and so does not sort chronologically. Under that provider those columns are
  stored as UTC ticks through a value converter. SQL Server keeps a real `datetimeoffset`.
