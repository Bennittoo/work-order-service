# AI usage

I used Claude (Claude Code) on this assignment. The brief asks for the prompts and the thinking
behind them. The thinking is below; the prompts, with what came back and what I did with each,
are in the appendix.

## How I used it

I set the arrangement before any code existed: **I make the design decisions, the tool argues with
them.** This repository is the basis of a technical interview, so nothing could ship that I was not
able to explain. Code I had not reasoned about would be a liability, not a saving.

In practice that meant I specified each piece, reviewed what came back, and sent several pieces back.
I wrote the code myself at first and moved to specifying and reviewing it, because reviewing is where
I am quickest and I wanted the remaining time for the reliability work and the video.

One deliberate choice at the start: I wrote my own plain-English reading of the brief **before**
opening the assignment PDF, so that comparing the two would show me where I had misread it rather
than having the gaps quietly filled in. That is how I found that Minimal APIs are mandatory, and that
the wording is "multiple effects" rather than "multiple status changes" — which changed a test,
because asserting on status alone would pass even if history had doubled.

## The decisions, and my reasoning

Every one of these is mine to defend, and each is written out in full in SOLUTION.md.

**The status lifecycle.** `Pending → InProgress → Completed`, with `Cancelled` reachable from either
open state, and both end states terminal. `Pending → Completed` is illegal because work cannot finish
without starting. `InProgress → Pending` is illegal because there is no un-starting; rework would be
a different concept than this brief describes. This is the one purely business judgement in the
project and I made it.

**Repeating a status is a success that writes nothing.** An at-least-once source will legitimately
report `InProgress` twice with two different event ids. Treating the second as an error would mean
the service reports failures for entirely normal upstream traffic. So it succeeds, and writes no
history entry, because history records changes.

**Where the rule is enforced.** I had been thinking about the *set* of legal transitions as the
interesting problem. The better question turned out to be where the rule lives, because there are two
entry points and they must not each hold a copy. The invariant worth protecting is not "transitions
are legal", it is that **status and history can never diverge** — so the method that assigns `Status`
is the method that appends the history entry, and the setter is private. When I later restructured
into layers I had to defend this decision against my own plan, and I kept it.

**History records `FromStatus` as well as `ToStatus`.** My call. The history is an audit trail, and an
audit trail you cannot check for gaps is not much of one.

**Two timestamps on every history entry.** `OccurredAt` from the reporter, `RecordedAt` from us.
Conflating them would make an event that arrived late indistinguishable from one that was processed
late.

**Persisted deduplication rather than an in-memory set.** The brief allowed either. An in-memory set
fails by re-applying every event the upstream retries after a restart, which is precisely the failure
the requirement exists to prevent, and it stops working entirely with two instances. It meets the
letter and not the intent.

**The dedup row and the change commit in one transaction.** An event is marked handled only if its
effect actually committed. Split them and you get one of two bugs: the event marked done with its
effect rolled back, or the effect applied twice.

**A full queue returns 503 with `Retry-After`.** Dropping loses an event already acknowledged with a
202, which makes the acknowledgement a lie. Blocking swaps a bounded queue for unbounded request
latency. Rejecting is honest, and it is only safe *because* processing is idempotent — the two
decisions hold each other up.

**One consumer, and a concurrency token anyway.** One consumer serialises event-driven changes. The
token is there for the API, not the worker, because the HTTP status endpoint is a second writer.

**`Description` required, not nullable.** A work order always has one, and carrying a null through to
every response is worse than rejecting a blank.

**Secrets out of `appsettings.json`.** Connection string and API key from user secrets, container
password from a gitignored `.env` with a committed example. The service refuses to start without a
key rather than starting silently unprotected.

**XML documentation on every public member**, with `GenerateDocumentationFile` on and warnings as
errors, so an undocumented public member fails the build. Wired into Swashbuckle, which means those
descriptions became the schema descriptions in the OpenAPI document.

**Three layered projects**, with the use cases in a manager. Dependencies point inward only, and
`Domain` references nothing at all, not even EF Core.

## Where I overrode it

**The anemic model.** I asked for a data-only model with all the logic in a manager, which is the
common enterprise shape. It argued that this would need a public setter on `Status`, turning the
invariant above from something the type enforces into a convention every caller has to remember. It
was right, and what went in instead is the split that gives me the layering without that loss: the
manager owns the use cases, the entity keeps its own invariant.

**`Description` nullable.** It made it nullable. I made it required.

**The deduplication pre-check.** It had left this out on purpose, with the unique key as the only
guard. Running the service showed every duplicate producing an error-level log with a full stack
trace, because duplicates were being discovered by letting the insert fail. For an at-least-once
source, redelivery is ordinary traffic, so ordinary traffic was writing error logs. I required a read
before the write. The unique key stays, because a read cannot close its own race.


**Three DTO shapes.** I asked for application models and API responses as separate types. With one
transport there is nothing for the third shape to absorb, so it is two: requests in, application
models out. A new field is two edits rather than three.

**The transactional inbox.** It proposed one, then withdrew it as out of scope. I agreed. It stays in
SOLUTION.md as considered and declined, which is more useful than half of it in code.

## What I caught

**The service had no route at `/`.** I ran it and got a 404. The template's placeholder had been
removed and never replaced, and the launch profile opens a browser there, so a reviewer would have
hit exactly the same thing on their first run. 66 tests had not caught it, because no test opens the
root in a browser.

**Error-level logs on ordinary traffic.** Described above. Found by reading the service's own output
rather than by a test failing.

**A stale solution file.** Visual Studio kept regenerating a `.slnx` listing only some projects, and
it takes precedence over the `.sln`. It made the IDE show an out-of-date structure and broke
root-level `dotnet build`. Gitignored now.

**The architectural mismatch in the reference codebase**, above.

## What it caught that I would have missed

Recorded plainly, because pretending otherwise would defeat the point of this document.

- **`DbContext` is scoped, a hosted service is a singleton**, so event handling needs its own scope.
  I know this. It is also the most common way hosted services go wrong, and having it named up front
  meant it was right the first time rather than the second.
- **An unhandled exception in `ExecuteAsync` stops the hosted service.** The try/catch belongs around
  each item, not around the loop, or one bad event silently ends all processing for the life of the
  process.
- **The `Enum` suffix on enum type names.** I asked for `StatusChangeOutcomeEnum`. It cited the
  Framework Design Guidelines, which say explicitly not to do that. I kept the `Enumerations` folder
  and dropped the suffix.
- **NuGet restore failed with a 401**, because this machine has a private feed configured. It would
  have failed the same way on a clean clone and looked like a broken repository. Fixed with a
  repository-level `NuGet.config`.

## What the tests caught, which neither of us predicted

Ten of the first seventeen integration tests failed on the first run, for three separate reasons, and
all three were real defects rather than bad assertions.

1. **SQLite refuses to `ORDER BY` a `DateTimeOffset`.** Its text encoding embeds the offset, so the
   text does not sort chronologically and EF will not translate the query at all. This broke every
   read that orders history or lists work orders, including a plain 404 lookup, because the failure
   is at translation time and needs no rows.
2. **A `Guid[]` with `Contains` in an EF query** binds to the `ReadOnlySpan` overload, which EF
   cannot translate, and surfaces as an unreadable type-load error. A `List<Guid>` binds correctly.
3. **One `SqliteConnection` shared between the request path and the background worker** produced
   `database is locked`. This is the one I would raise unprompted in an interview. "SQLite in memory
   for speed" is the reflex answer, and it is wrong for a service whose entire point is a background
   writer running alongside the API: the in-memory version was not testing the concurrency, it was
   failing on it. The tests use a temporary SQLite file with WAL instead.

## What I verified by hand

Every claim in the README and SOLUTION.md was checked against a running service rather than inferred.

- The migration applied to a real SQL Server, including the clustered index placement, which only
  fails at apply time.
- The same `eventId` submitted three times: one status change, one history entry, one
  `ProcessedEvents` row.
- Queue exhaustion, with capacity set to one and forty events fired at once: sixteen accepted,
  twenty-four refused with 503 and `Retry-After`.
- The concurrency token, by driving two contexts at the same row: the stale writer gets a
  `DbUpdateConcurrencyException` and its write leaves nothing behind.
- `occurredAt` and `recordedAt` coming back as different values, which is the two-timestamp design
  visibly working.
- The generated OpenAPI document, field by field. It was wrong twice before it was right: enums
  documented as integers while the API emits strings, and a document-level security requirement
  claiming the open read endpoints need an API key.
- A clean clone from GitHub, built and fully tested with no setup at all.

---

# Appendix: the prompts, and what came back

These are the prompts that shaped the design, in the order they were asked, given as the substance
of what I asked rather than as raw transcript. The phrasing is tightened; the content, the sequence
and the answers are as they happened. Each entry says why I asked it that way, because the prompt is only half of the
decision. A working log was kept alongside the build and this was written from it.

---

### 1. Check my reading of the brief, do not replace it

> "Here is my own plain-English breakdown of this assignment: the domain, the three tasks, what I
> think each task is actually testing, and the six decisions I believe I have to make and defend.
> Tell me where I am wrong or where I am under-weighting something. Do not restate the brief back to
> me."

I wrote my reading before opening the PDF on purpose. If I had asked it to read the brief and tell
me what to build, any gap in my understanding would have been quietly filled in and I would never
have known the gap was there.

The six I had already identified as mine to decide, before any of this: the legal transition set and
where the rule is enforced, what a history row stores, how duplicate events are detected, what
happens when the queue is full, which failures are permanent and which are transient, and what
database the tests run against. That list is the architecture of this service, and finding it is the
part that has to be yours. Everything after it is argument.

**What came back:** the breakdown was accurate, but two things were under-weighted. Concurrency: two
progress events for the same work order processed together give a lost update, and I need an answer
even if the answer is "one consumer, so it cannot happen". Durability honesty: an in-memory queue
drops every accepted-but-unprocessed event on restart, so the endpoint returns 202 and then loses
the work. The brief mandates in-memory, so it is acceptable, but it has to be named rather than left
to be found.

It also reframed my six decisions as the traps behind them. The one that changed the design: on
transitions, the question is not which moves are legal, it is where the rule is enforced, because
there are two entry points and they must not each hold a copy.

**What I did:** kept my breakdown, took the reframing.

---

### 2. Now the actual brief, and the differences

> "Here is the assignment PDF. I worked from my own reading first, deliberately. List the deltas
> against my summary, and for each one say what it changes about the design rather than just quoting
> the requirement."

**What came back:** Minimal APIs are mandatory, not controllers, and the consequence I had missed is
that Minimal APIs have no automatic model validation, so validation is something I have to add
deliberately. Events may reference the external id rather than the internal one, which makes the
external key a real design decision and "unknown work order" a likely error path. Persisting dedup
state is explicitly optional, and the explanation is what is graded. The wording is "should not
produce multiple effects", not "should not change status twice", so a duplicate must not append a
second history row either.

**What I did:** that last one changed a test. Asserting on status alone passes even when history has
doubled, which is the exact bug the requirement exists to prevent.

---

### 3. Stress-test the four I had left open

> "Four things are still open and they are mine to defend: the legal transition set and where it is
> enforced, stored current status versus derived from history, in-memory versus persisted dedup and
> where the transaction boundary sits, and one consumer or many. Argue each of them with me. Give me
> the argument, not the conclusion, and tell me what each option costs."

These are four of the six from my own breakdown above. I asked for the arguments rather than the
answers because I have to make these arguments myself, and a conclusion I cannot reconstruct is
worth nothing to me. The costs matter more than the choices: every one of these has a defensible
alternative, and what separates them is what you are willing to pay.

**Where each landed, and why.** Transitions are enforced on the entity, because the invariant is not
"transitions are legal", it is "status and history can never diverge", so the method that changes
status is the only way to change it and appends the history entry itself. Status is stored rather
than derived, because the brief explicitly asks for a filtered list and deriving turns that one
query into a latest-row-per-order subquery. Dedup is persisted with a unique key and commits in the
same transaction as the change, so an event is marked handled only if its effect actually landed.
One consumer, with a concurrency token anyway, because the HTTP status endpoint is a second writer
even when the worker is not.

**What I paid for each, since that is the real test of whether a decision is mine.** The enforcement
point costs me a private setter and an entity that is not a plain data model, which is unfashionable
and which I had to defend later against my own restructure plan, in entry 7, where I kept it.
Storing status costs a drift risk, which is why there is a test asserting `Status` equals the last
history entry's `ToStatus` across a run of applied, no-op and rejected changes. Persisted dedup costs
a table that grows without bound and needs a retention policy I have not built, and I took it anyway,
because the brief allows in-memory and in-memory re-applies every retried event after a restart,
which is the precise failure the requirement exists to prevent. One consumer costs throughput, and
the scale answer I did not build is partitioning by hash of work order id across N channels to keep
per-order ordering.

---

### 4. Change the division of labour

> "From here you scaffold and I review each piece. Reviewing is where I am fastest, and I would
> rather spend the remaining time on the reliability work and the video than on typing. The
> constraint does not change: nothing ships that I cannot explain, so every piece gets reviewed
> before the next one starts."

**What I did:** eight steps, each reviewed before the next began. Entries 5 and 6 below are two of
the reviews that sent a step back. The review I would point at first, though, is the one where I
changed nothing: when my own restructure in entry 7 would have moved the transition rule off the
entity, I kept it where it was.

---

### 5. Overriding it: Description should not be nullable

> "On Description being nullable, I disagree. A work order always has one. Make it required, and
> reject blank the same way a blank external id is rejected."

**Why:** a nullable field the domain never actually allows to be null is a null that every response
then has to carry for no reason.

---

### 6. The pre-check, raised rather than taken

> "Every duplicate is writing a full SqlException stack trace to the logs, because we discover the
> duplicate by letting the insert fail. For an at-least-once source that is normal traffic, not an
> exception. Add the existence pre-check, and keep the unique index as the guard."

**The reasoning I want to be able to give:** the check is an optimisation, not the guard. It cannot
close the window between the read and the write, so the unique key still has to exist and its
violation still has to be caught. What the check buys is that the ordinary case, redelivery from an
at-least-once source, stops being handled by throwing.

---

### 7. Restructure into layers, and defend it against me

> "I want an Application project holding the managers with the business logic, application models
> mapping the domain, and Validations, Abstractions and Enumerations folders. One responsibility per
> file: a model holds structure, a manager holds behaviour. Secrets out of appsettings and into user
> secrets. XML documentation on every property and enum, surfaced in Swagger. Requests and Responses
> folders in the API. Push back on anything here you think is wrong."

I asked for the pushback explicitly because this came late in the build, and a late restructure that
nobody argues with is how you break a working service.

**What came back, and I took both:** the `Enum` suffix on type names was dropped, because the
Framework Design Guidelines say not to suffix them that way and a convention-focused .NET reviewer
reads it as unfamiliarity rather than care. The folder stayed. And the anemic model was refused:
moving the transition rule off the entity would have required a public setter on `Status`, turning a
type-enforced invariant back into a convention. What went in instead is the manager owning the use
cases and the entity keeping its invariant.

**Two consequences the plan had not anticipated,** both forced by dependency direction: the
validation rules moved to Application while the endpoint filter stayed in the API, because the
filter is an `IEndpointFilter` and Application must not reference ASP.NET Core; and persistence
moved into Application, because the manager depends on the `DbContext` and the reference would
otherwise have been circular.
