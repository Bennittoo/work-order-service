# Solution

Design decisions, and what they cost.

## Shape of the service

Three projects.

| Project | Holds | References |
| --- | --- | --- |
| `Domain` | Entities and the status lifecycle | Nothing |
| `Application` | Use cases, models, validation rules, ports, persistence | `Domain` |
| `Api` | HTTP: requests, responses, endpoints, filters, hosted service | `Application` |

**The Clean Architecture dependency rule is honoured: dependencies point inward only**, and `Domain`
references nothing at all, not even EF Core. The concurrency token is the clearest evidence of the
rule being taken seriously rather than stated: `RowVersion` is a plain `byte[]` on the entity with no
`[Timestamp]` attribute, configured in the persistence layer instead, precisely so the domain stays
provider-agnostic.

### Where the business logic lives, and why it is split

`WorkOrderServiceManager` owns the use cases: loading, the transaction boundary, deduplication, and
recording what happened. The endpoints and the background processor both go through it, and neither
touches a `DbContext`.

What the manager does **not** own is the status rule itself. That stays on
`WorkOrder.ApplyStatus`, and the reason is the invariant being protected. It is not "transitions are
legal", it is **status and history can never diverge**. Only the entity can guarantee that: the
method that assigns `Status` is the same method that appends the history entry, and `Status` has a
private setter so there is no other way in. Moving the rule into the manager would need a public
setter, at which point the invariant is enforced by every caller remembering to go through the
manager rather than by the type.

So the division is: **use cases orchestrate, entities hold invariants.** That is also what Clean
Architecture prescribes, rather than a data-only model with the rules in a service.

### What is not split

There is no separate `Infrastructure` project. Persistence lives in `Application`, because the
manager depends on the `DbContext` directly and a separate project would only add a hop. A second
adapter behind the same port would change that, and the ports already exist.

The codebase has exactly two interfaces, `IProgressEventQueue` and `IUniqueConstraintDetector`, and
both exist for the same reason: something provider or transport specific had to be replaceable
without changing the logic that depends on it. That is the only justification I would accept for an
abstraction at this size.

There is no MediatR, no CQRS, no repository layer over EF Core, and no event sourcing. Reasons in
[What I chose not to build](#what-i-chose-not-to-build).

## The status lifecycle, and where it is enforced

```
Pending ──▶ InProgress ──▶ Completed
   │             │
   └─────────────┴────────▶ Cancelled
```

`Completed` and `Cancelled` are terminal. `Pending → Completed` is illegal: work cannot finish
without starting. `InProgress → Pending` is illegal: there is no un-starting, and rework would be a
different concept than this brief describes.

**Repeating the current status is a no-op success, not a violation.** An at-least-once event source
will legitimately send two distinct events that both say `InProgress`. Treating that as an invalid
transition would produce failures for entirely normal upstream behaviour. It succeeds and writes no
history entry, because history records changes.

The transition table is data, not a `switch`. That buys rejection messages that enumerate what *is*
allowed, and a test that walks every enum value to prove none is silently unmapped.

Enforcement lives in one method on the entity, `WorkOrder.ApplyStatus`, with `Status` behind a
private setter and the history collection exposed read-only. The reason it is there rather than in a
validator service is the invariant being protected. It is not "transitions are legal", it is
**status and history can never diverge**. So the method that changes status is the same method that
appends history, and it is the only way to change status. A separate validator leaves a door open
where someone assigns `Status` directly and no history is written.

Both entry points reach that method through `WorkOrderServiceManager`, so the coordination is
written once and the rule is enforced once.

`ApplyStatus` returns three outcomes rather than a bool, and throws in two cases: an undefined enum
value, and a `Creation` source. That split matters. `Rejected` is normal traffic that the processor
must handle gracefully; a throw means the caller has a bug. Collapsing them would let a defect be
swallowed as though it were an invalid transition.

## Data model

`WorkOrders` carries the current status; `WorkOrderStatusHistory` is the append-only ledger beside
it.

**Status is stored, not derived from history.** The deciding factor is a hard requirement: listing
with an optional status filter. Deriving would put a latest-row-per-work-order subquery in the one
query the brief explicitly asks for. The cost of storing it is two things that could drift, and the
enforcement decision above is what pays that cost. A test asserts `Status` always equals the last
history entry's `ToStatus` across applied, no-op and rejected changes.

**History records `FromStatus` and `ToStatus`.** A trail of destinations alone cannot be checked for
gaps. `FromStatus` is nullable, and null only on the creation entry. Creation writes an entry
because otherwise the invariant above fails for every brand-new work order and the test would need a
special case.

**Two timestamps per entry.** `OccurredAt` is when the change happened according to whoever reported
it, taken from the event where there was one; `RecordedAt` is when this service persisted it. They
are equal for API-driven changes. Conflating them would make an event that arrived late
indistinguishable from one that was processed late.

**`Source` and `EventId`** say who caused the change and which event it came from. An audit trail
that cannot answer "where did this come from" is half an audit trail.

Other choices worth naming:

- Enums persist as text, so the table is readable and renumbering the enum cannot silently
  reinterpret existing rows.
- `ExternalId` is unique and indexed. It is how an external system addresses a work order, because
  an external system knows its own key rather than ours.
- There is no `DbSet<StatusHistoryEntry>`. History is part of the work order aggregate and reachable
  only through it, so the append-only rule cannot be bypassed by anyone holding a `DbContext`.
- GUID primary keys are `Guid.NewGuid()`. Random GUIDs fragment a clustered index, which is why
  `ProcessedEvents` has a non-clustered primary key and is clustered on `ProcessedAt` instead. On
  .NET 9 or later `Guid.CreateVersion7` would remove the problem at source.

## Background processing

`POST /api/progress-events` validates the request, puts a message on a bounded in-memory
`Channel<T>`, and returns `202`. A hosted `BackgroundService` is the single consumer.

**The accept endpoint touches no database.** Whether the work order exists and whether the
transition is legal are decided where the work is done. A 202 means the event was taken, not that it
was valid. Checking existence up front would add a round trip to the hot path and still race.

**The queue is bounded, and a full queue returns 503 with `Retry-After`.** The three options were
block, drop, or reject. Dropping loses an event already acknowledged with a 202, which makes the
acknowledgement a lie. Blocking trades a bounded queue for unbounded request latency and moves the
backpressure into connection exhaustion. Rejecting tells the caller the truth, and it is only safe
because processing is idempotent, so the resubmission being requested cannot double-apply. Those two
decisions hold each other up.

**The queue sits behind an interface deliberately.** `IProgressEventQueue` has three members and no
knowledge of channels, so replacing `ChannelProgressEventQueue` with a RabbitMQ or Azure Service Bus
consumer is one registration in `Program.cs`. Everything that makes processing correct, the single
`SaveChangesAsync`, the deduplication key and the outcome recording, lives in the manager and does
not move.

**The processor owns delivery, not decisions.** Scoping, retrying and logging are its job; what to do
with an event is `WorkOrderServiceManager.ApplyProgressEventAsync`. That split is why the same
idempotency logic is covered by application tests with no HTTP layer involved.

**A scope per event.** The processor is a singleton and the manager holds a scoped `DbContext`, so
each event is handled inside its own `IServiceScopeFactory` scope.

**try/catch per item, not around the loop.** An exception escaping `ExecuteAsync` stops the hosted
service, which would silently end all event processing for the life of the process.

**Graceful drain.** `StopAsync` completes the channel writer before the base class signals the
stopping token, and the reader is passed `CancellationToken.None`, so shutdown processes what was
already accepted rather than abandoning it. A test enqueues 50 events, stops the processor, and
requires all 50 to have been processed.

## Idempotency

`ProcessedEvents` has one row per event the processor has finished with, keyed on `EventId`.

**The key is the guard.** The insert of that row happens in the same `SaveChangesAsync` as the
status change and the history entry, so all three share one transaction. An event is marked handled
only if its effect actually committed, and a duplicate cannot be applied twice even under a race,
because the second insert violates the key.

**There is also a read before the write, and it is only an optimisation.** Redelivery from an
at-least-once source is ordinary traffic, not an exception, and discovering it by letting an insert
fail meant every duplicate produced an error-level log with a stack trace. The existence check keeps
the common case off that path. It cannot close the window between the read and the write, which is
why the unique key still exists and its violation is still caught. Both are needed and they do
different jobs.

The brief allows an in-memory set instead. I did not use one because its failure mode is that a
restart re-applies every event the upstream retries, which is precisely the failure the requirement
exists to prevent, and because a per-instance set stops working entirely the moment there are two
instances. An in-memory set satisfies the letter of the requirement and not its intent.

**Cost I am accepting:** `ProcessedEvents` grows without bound. A real deployment needs a retention
policy, most simply a scheduled delete of rows older than the upstream's redelivery window. I have
not built one.

`Rejected` and `WorkOrderNotFound` are also recorded as processed. Neither will ever succeed on
retry, so recording them is what stops an unknown identifier being reprocessed forever, and it
leaves a trace that the event arrived. That makes the table double as an audit of every event
received and what was decided about it.

## Concurrency

**One consumer.** It serialises every event-driven change, so there are no lost updates between
events and no per-work-order locking.

**`RowVersion` exists for the API, not the worker.** The HTTP status endpoint is a second writer, so
a user changing status while the worker processes an event is a genuine concurrent write even with a
single consumer. On conflict the worker re-reads and re-evaluates the transition against current
state, bounded by `MaxProcessingAttempts`; the endpoint returns 409.

The token is verified live against SQL Server: a stale writer gets a
`DbUpdateConcurrencyException`, and its write leaves nothing behind, so there is no partial status
change and no orphan history row. The HTTP 409 itself has not been forced end to end, because the
conflict window sits between the read and the write inside a single request and cannot be entered
from outside.

Re-reading is the right response rather than forcing the write through: if the work order moved on,
the transition the event proposed may no longer be legal, and that judgement belongs to the state
machine.

**Out-of-order events** are largely handled by the state machine for free. Because `Completed` is
terminal, a late `InProgress` is rejected as an illegal transition, which is the correct outcome.
The general solution would be to gate on `OccurredAt` against the current status's timestamp. I have
not built it, because the state machine covers the cases that arise here.

**Scaling past one consumer** would mean partitioning by hash of the work order id across N channels
so that all events for one work order land on the same consumer, preserving per-work-order ordering.
Not built, because one consumer is ample for rollout tracking volumes.

## Error handling

The distinction that drives everything is permanent versus transient.

| Case | Response | Why |
| --- | --- | --- |
| Malformed or invalid request | 400 with per-field errors | Never reaches the queue |
| Missing or wrong API key | 401, before validation runs | No point validating a body we will not act on |
| Duplicate `externalId` | 409 | Caught from the unique index, not a pre-check that would race |
| Illegal transition | 409 with the allowed moves named | Conflicts with current resource state |
| Unknown work order, from an event | Recorded as `WorkOrderNotFound` | Will never succeed on retry |
| Illegal transition, from an event | Recorded as `Rejected` with the reason | Will never succeed on retry |
| Concurrency conflict | Re-read and retry, bounded; 409 on the endpoint | Transient and resolvable |
| Transient database fault | Retried by EF Core's execution strategy | `EnableRetryOnFailure`, so the processor does not reimplement it |
| Anything else in the processor | Logged at error, event abandoned | Better than taking the host down |

Validation is split between the layer that owns the rules and the layer that owns the protocol. The
rules are in `Application/Validations`, which knows nothing about ASP.NET Core; a generic endpoint
filter in the API runs them and turns failures into a `ValidationProblem`. Minimal APIs do not
validate request bodies the way MVC model binding does, and that gap has to be closed deliberately.

Field length limits live in one `FieldLengths` class shared by the EF configurations and the
validators, since two copies of those numbers is how you get a 500 on input that passed validation.

## Testing

Three suites, one per production project. Domain tests cover the status rules with no database.
Application tests cover the use cases against SQLite, including the idempotency requirement, without
an HTTP layer. API tests drive the real application through `WebApplicationFactory`.

Splitting the manager out is what made the middle suite possible: the idempotency behaviour can now be
asserted directly, rather than only through a 202 and a poll.

The integration tests use SQLite in a temporary file rather than in memory. In-memory SQLite lives
inside a single open connection, and this service has two concurrent writers by design, so sharing
one connection produced `database is locked` rather than the behaviour under test. That is the
sharpest example of why "SQLite in memory for speed" is a trade-off and not a free win.

What the integration tests do not cover, and why, is listed in the README: concurrency conflicts
(SQLite has no `rowversion`) and the migration itself (it contains SQL Server specifics, so the test
schema is built from the model instead). The migration was applied to a real SQL Server by hand.

Tests are written Arrange, Act, Assert, and named as a sentence describing the behaviour rather than
the method under test, so a failure reads as a broken rule rather than a broken function.

Provider-specific behaviour sits behind `IUniqueConstraintDetector`, so the tests swap the
interpretation of a unique-key violation rather than the logic that depends on it.

### Verified by hand, beyond the suite

Everything the suite cannot reach was run against the containerised SQL Server rather than assumed:

- The documented setup end to end with no configuration overrides: `docker compose up`, the
  migration, then the service.
- The same `eventId` submitted three times, giving one status change, one history entry and one
  `ProcessedEvents` row.
- Queue exhaustion, by starting with `QueueCapacity=1` and firing forty events concurrently: 16
  accepted and 24 refused with 503 and `Retry-After`.
- The concurrency token, as described above.
- The generated OpenAPI document, checked field by field rather than eyeballed in the UI.

## Configuration and secrets

Nothing secret is committed. The connection string and the API key come from user secrets, and the
container password from a gitignored `.env` with a committed `.env.example` documenting it. The
README carries the three commands.

The cost is honest: a reviewer has one setup step that is not a single command. The alternative was a
committed password labelled as non-secret, which works but teaches the wrong habit and would be the
first thing to go wrong when someone copied the pattern into something real. Startup validation
refuses to run without an API key rather than starting silently unprotected.

## Documentation as part of the contract

Every public type, member and enumeration member carries XML documentation, and
`GenerateDocumentationFile` is on with warnings as errors, so an undocumented public member is a
build failure rather than an intention.

That is not only for readers of the code. Swashbuckle is wired to the generated XML, so the
descriptions become the schema descriptions in the OpenAPI document: what `occurredAt` means, why
`fromStatus` is nullable, and what each status implies are all visible in Swagger without opening the
source.

## What I chose not to build

- **MediatR or CQRS.** Four endpoints and one background handler. The indirection would cost more
  than the coupling it removes.
- **A repository layer over EF Core.** `DbContext` is already a unit of work and `DbSet` is already
  a repository. Wrapping them adds a layer that has to be maintained and mocked and buys nothing
  here.
- **An infrastructure project.** Persistence sits in `Application` because the manager depends on
  the `DbContext` directly. A second adapter behind the same port would earn the split.
- **A data-only model with the rules in the manager.** The common enterprise shape, and it would
  cost the one invariant this design is built on. Explained above.
- **A real message broker.** The brief specifies an in-memory queue and rules out external
  messaging. `IProgressEventQueue` is what makes that a swap rather than a rewrite: a RabbitMQ or
  Azure Service Bus implementation replaces `ChannelProgressEventQueue` and nothing else changes
  shape. What would genuinely change is the acknowledgement point, because a broker gives you
  redelivery, which is what would finally make the retry loop earn its keep.
- **A transactional inbox** (persist the event on receipt, queue its id, mark it done after
  processing). This is the correct production answer to the durability gap, and it was tempting. It
  is scope the brief did not ask for.
- **Event sourcing.** Deriving status from the history is the same idea, and it loses the simple
  filtered list query for a benefit this service does not need.
- **Migration on startup.** Convenient in a demo, but it means every instance races to alter the
  schema on deploy and gives the application permissions it should not hold. The README asks you to
  run one command instead.
- **A `ProcessedEvents` retention job.** Named as a known gap above rather than half-built.

## Known limitations

1. **An in-memory queue loses accepted-but-unprocessed events if the process dies.** Shutdown drains
   the queue, so an orderly stop is safe; a crash or a kill is not. Events already answered with a
   202 would be lost. The fixes are a durable broker, RabbitMQ or Azure Service Bus behind the
   existing `IProgressEventQueue`, or the transactional inbox above. This is the most important
   trade-off in the service and it is a direct consequence of the brief's constraint.
2. **`ProcessedEvents` grows without bound.**
3. **One consumer caps throughput**, deliberately. Partitioning is the way out.
4. **Random GUID primary keys** fragment clustered indexes. Mitigated on `ProcessedEvents`, not on
   `WorkOrders`.
5. **The API key is a single shared secret** with no rotation, per-caller identity, or rate
   limiting. It is what the brief listed as an optional extra and it is the right size for that. A
   service taking traffic from several external systems wants per-caller identity instead, which
   means JWT bearer tokens or mutual TLS. That is not only about authentication: it would let the
   audit trail record *which* upstream system reported a change rather than merely that a progress
   event did, and it would make revocation and rate limiting per-caller rather than
   all-or-nothing.
6. **No pagination beyond a page number.** Deep paging with `Skip` degrades; keyset pagination would
   be the answer at scale.
