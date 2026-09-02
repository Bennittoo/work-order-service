# Work Order Service

A backend service for managing work orders on network infrastructure rollouts, and for ingesting
progress events from external systems asynchronously.

.NET 8, ASP.NET Core Minimal APIs, EF Core, SQL Server, xUnit.

Design decisions and trade-offs are in [SOLUTION.md](SOLUTION.md). How AI tooling was used is in
[AI_USAGE.md](AI_USAGE.md).

## Running it locally

### 1. Supply the secrets

Nothing secret is committed, so there are three values to set once. This is the only setup step that
is not a single command.

Choose a SQL Server password and put it in a `.env` file for Docker Compose:

```bash
cp .env.example .env
```

Then edit `.env` and replace the placeholder. SQL Server rejects weak passwords, so it needs upper
case, lower case, a digit and a symbol.

Now give the application the same password in its connection string, and an API key, through user
secrets:

```bash
dotnet user-secrets --project src/WorkOrderService.Api set "ConnectionStrings:WorkOrders" "Server=localhost,14330;Database=WorkOrders;User Id=sa;Password=THE_PASSWORD_FROM_YOUR_ENV;TrustServerCertificate=True;Encrypt=False"
```

```bash
dotnet user-secrets --project src/WorkOrderService.Api set "ApiKey:Value" "any-value-you-like"
```

User secrets are stored in your own profile, outside the repository. The service refuses to start
without an API key configured, rather than starting silently unprotected.

One thing worth knowing, because it is a real trap: **ASP.NET Core loads user secrets only in the
Development environment.** `dotnet run` sets that through the launch profile, and the EF Core tools
default to it, so the steps above work as written. Anywhere else, including a published build or a
container, supply the same values as environment variables instead:

```bash
export ConnectionStrings__WorkOrders="..."
export ApiKey__Value="..."
```

### 2. Start SQL Server

```bash
docker compose up -d
```

This starts SQL Server 2022 on `localhost:14330`. Port 14330 rather than 1433 is deliberate: if you
already have SQL Server installed it is listening on 1433, Windows allows both to bind it, and your
local instance wins the connection while the container sits there unreachable. Wait for the container
to report healthy:

```bash
docker compose ps
```

To use an instance you already have instead, skip the container and point the connection string at
it. A trusted connection needs no password:

```bash
dotnet user-secrets --project src/WorkOrderService.Api set "ConnectionStrings:WorkOrders" "Server=.\\SQLEXPRESS;Database=WorkOrderService;Trusted_Connection=True;TrustServerCertificate=True"
```

### 3. Create the schema

The application does not migrate on startup, deliberately (see SOLUTION.md). Apply the migration
yourself:

```bash
dotnet tool restore
```

```bash
dotnet ef database update --project src/WorkOrderService.Application --startup-project src/WorkOrderService.Api
```

### 4. Run

```bash
dotnet run --project src/WorkOrderService.Api
```

The service listens on `http://localhost:5045`. A browser opening the root is redirected to Swagger
UI at `/swagger`; the OpenAPI document is at `/swagger/v1/swagger.json`. The schema descriptions in
Swagger come from the XML documentation on the models and enumerations.

### 5. Run the tests

```bash
dotnet test
```

No database and no secrets required: the domain and application tests bring their own SQLite file,
and the integration tests supply their own configuration. See [Testing](#testing) for what that does
and does not cover.

## Configuration

| Setting | Where it comes from | Purpose |
| --- | --- | --- |
| `ConnectionStrings:WorkOrders` | user secrets | Database connection. No default. |
| `ApiKey:Value` | user secrets | Key required on write endpoints. No default; startup fails without it. |
| `ApiKey:HeaderName` | `appsettings.json` | Header carrying the key. Defaults to `X-Api-Key`. |
| `ProgressEvents:QueueCapacity` | `appsettings.json` | Bounded queue size for accepted events. Defaults to 1000. |
| `ProgressEvents:MaxProcessingAttempts` | `appsettings.json` | Reprocessing attempts after a concurrency conflict. Defaults to 3. |
| `MSSQL_SA_PASSWORD` | `.env` | The container's SQL Server password. |

Any of these can also be supplied as environment variables, which is how a deployment would do it.
Use a double underscore for nesting, for example `ApiKey__Value`.

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

The examples below use `YOUR_API_KEY` for the value you set in step 1.

### Create a work order

```bash
curl -X POST http://localhost:5045/api/work-orders -H "Content-Type: application/json" -H "X-Api-Key: YOUR_API_KEY" -d '{"externalId":"EXT-1001","siteCode":"JHB-042","description":"Install equipment at JHB-042"}'
```

`201 Created`, with `Location: /api/work-orders/{id}`:

```json
{
  "id": "f1703e5c-272e-447c-90b5-b74fb320b8b9",
  "externalId": "EXT-1001",
  "siteCode": "JHB-042",
  "description": "Install equipment at JHB-042",
  "status": "Pending",
  "createdAt": "2026-09-02T07:21:09.2804168+00:00",
  "updatedAt": "2026-09-02T07:21:09.2804168+00:00",
  "statusHistory": [
    {
      "fromStatus": null,
      "toStatus": "Pending",
      "occurredAt": "2026-09-02T07:21:09.2804168+00:00",
      "recordedAt": "2026-09-02T07:21:09.2804168+00:00",
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
curl -X PUT http://localhost:5045/api/work-orders/{id}/status -H "Content-Type: application/json" -H "X-Api-Key: YOUR_API_KEY" -d '{"status":"InProgress","details":"Crew on site"}'
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
      "createdAt": "2026-09-02T07:21:09.2804168+00:00",
      "updatedAt": "2026-09-02T07:21:11.6939943+00:00"
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
curl -X POST http://localhost:5045/api/progress-events -H "Content-Type: application/json" -H "X-Api-Key: YOUR_API_KEY" -d '{"eventId":"c32892fd-1d8b-4eb8-ab31-aea5d9077f2b","workOrderExternalId":"EXT-1001","newStatus":"InProgress","occurredAt":"2026-09-02T07:00:00Z","details":"Crew dispatched"}'
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
  WorkOrderService.Domain         Entities and the status lifecycle. References nothing.
    Enumerations/                 Statuses, outcomes and change sources.
  WorkOrderService.Application    The use cases, and everything they need.
    Managers/                     WorkOrderServiceManager: the use cases themselves.
    Models/                       What the use cases accept and return.
    Validations/                  The rules, and the field lengths they share with EF Core.
    Abstractions/                 Ports: the event queue, and unique constraint detection.
    Enumerations/                 Application-level outcomes.
    Persistence/                  DbContext, EF configurations, migrations.
  WorkOrderService.Api            HTTP only.
    Requests/  Responses/         The wire contract.
    Endpoints/                    Minimal API route definitions.
    Validation/                   The endpoint filter that runs a request's rules.
    Security/                     The API key filter.
    Processing/                   The channel queue and the hosted service.
    Persistence/                  The SQL Server adapter for unique constraint detection.
    Swagger/                      OpenAPI document filters.
tests/
  WorkOrderService.Domain.Tests       Status rules, no database.
  WorkOrderService.Application.Tests  The use cases, over SQLite.
  WorkOrderService.Api.Tests          The running application, end to end.
```

Dependencies point inward only: `Api` depends on `Application`, `Application` depends on `Domain`,
and `Domain` depends on nothing at all.

## Testing

`dotnet test` runs the three suites. They are written Arrange, Act, Assert, and named as sentences
describing behaviour rather than the method under test.

- **Domain tests** cover the status rules with no database at all.
- **Application tests** cover the use cases against SQLite, including the idempotency requirement.
- **API tests** drive the real application through `WebApplicationFactory`.

The two database-backed suites use **SQLite**, in a temporary file created per test class and deleted
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
