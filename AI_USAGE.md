# AI usage

I used Claude (Claude Code) on this assignment. This document records what I asked it, what I did
with the answers, and where my judgement and its suggestions parted company.

## How I worked

**The arrangement, set before any code was written:** I make the design decisions, the tool reviews
and challenges them. The reason is practical. This repository is the basis of a technical interview,
so nothing can ship that I cannot explain, and code I have not reasoned about is a liability rather
than a saving.

**The division of labour, which changed partway through.** I started writing the code myself and
switched to specifying and reviewing it. Reviewing is where I am fastest, and I wanted the remaining
time for the reliability work and the video rather than for typing. What did not change is that every
piece was read and questioned before the next one started, and several were sent back.

**One deliberate choice at the outset:** I wrote my own plain-English reading of the brief *before*
opening the assignment PDF, so that comparing the two would show me where my understanding was wrong
instead of having the gaps quietly filled in.

## The decisions, and where each came from

| Decision | Origin |
| --- | --- |
| Transition set and terminal statuses | Mine, confirmed |
| Enforcement on the entity, not a service | Its argument, which changed my mind |
| Status stored rather than derived | Its recommendation, adopted |
| Persisted deduplication over an in-memory set | Its recommendation, adopted |
| Single consumer plus a concurrency token | Its recommendation, adopted |
| `FromStatus` and `ToStatus` on history | Mine |
| `Description` required, not nullable | Mine, overriding it |
| Deduplication read before the write | Mine, after running the service |
| Drop the production codebase as a reference | Mine |
| No `Enum` suffix on enum type names | Its citation of the guidelines, accepted |
| Manager owns use cases, entity keeps the rule | Its counter-proposal to my plan, accepted |
| Two DTO shapes rather than three | Its counter-proposal, accepted |
| Secrets out of `appsettings.json` | Mine |
| XML documentation on every public member | Mine |
| Layered projects with a Managers folder | Mine |

## The prompts, in order

Quoted as typed, including the untidy ones. A tidied log would not be the thing that was asked for.

### 1. My own reading of the brief, with instructions to coach rather than build

> "Plain-English breakdown of the assignment (so we're aligned before building): ... How I want to
> work: I'll face a technical interview that deep-dives this exact project, so coach me and review
> my code, but let me build it and understand every decision. Let's plan the architecture and data
> model first — I'll propose approaches, you refine."

It confirmed the breakdown and flagged two things I had under-weighted: concurrency between the API
and the worker, and the fact that an in-memory queue silently loses accepted events on restart.

It also reframed my list of open questions in a way that changed my thinking. I had been treating
the *set* of legal transitions as the interesting problem. The better question is *where the rule is
enforced*, because there are two entry points and they must not each hold a copy.

### 2. The assignment PDF, with no accompanying instruction

Checking my reading against the source caught two things I had missed. Minimal APIs are mandatory,
which means request validation has to be added deliberately rather than inherited from model binding.
And the wording is "multiple effects", not "multiple status changes", so a duplicate must not append
a second history entry either. That second one changed a test: asserting on status alone would pass
even with duplicated history.

### 3. The recruiter's email, pasted in full

It carries requirements the PDF does not, including this document, and it moved the timebox from
about four hours to seventy-two. That changed what was worth building.

### 4. On capturing this document

> "I hope we will be able to review the AI_USAGE.md and correct the structure when we finish the
> project."

The answer that mattered was that the editing could wait but the capture could not. A raw working log
was started at that point, and this document was edited down from it at the end.

### 5. On reusing a codebase I already work on

> "Can we also use the ejm ai project to assist us in architecture of the api?"

I asked whether I could take architectural cues from a production .NET codebase I work on. It
inspected it and advised against: that codebase uses Dapper and stored procedures with no EF Core,
and controllers rather than Minimal APIs, so the parts most worth copying are exactly the parts that
contradict this brief.

### 6. Dropping it

> "If the Ejm is misleading, please lets not use it as a reference."

Dropped entirely. A reference you have to keep correcting is not a reference. It is also my
employer's code, and this repository is public.

### 7. Starting the work

> "start the raw AI usage log now,
>
> And we can start building our project here: "C:\Users\...\source\repos""

### 8. The four open design decisions

> "give me your recommended answers for the four decisions"

The four I had left open: the transition set and its enforcement point, stored versus derived status,
the deduplication approach and its transaction boundary, and one consumer or many.

### 9. Adopting them

> "I'm happy with your decision, you corrected lot of things I could have went wrong on"

I adopted all four, and that is the honest record. What I took was the arguments rather than the
conclusions, which is why SOLUTION.md states each one in my own words: those are what I have to
defend.

The argument that changed my thinking most is the invariant framing. The thing being protected is not
"transitions are legal", it is that **status and history can never diverge**, and that is what puts
the history append inside the same method as the status assignment.

### 10. Changing the division of labour

> "you scaffold it and I'll review each piece (The thing is that if I write myself it's going to
> take time, it's not like I don't know coding, I'll review the code and give my judgement from
> there, you know I've been doing this in other project, reviewing is what I'm good at)
>
> And with regards to the from and to status, since the history is acting as an Audit trail, so that
> will be a good idea to have."

Two things in one message: the working change, and a decision of my own. History records both
`FromStatus` and `ToStatus`, because an audit trail you cannot check for gaps is not much of one.

### 11. Working through the build

> "continue with step 2" ... "continue with step 7"

Each step produced code, which I read before the next one started.

### 12. Overriding it on nullability

> "With regards to the description being nullable, I think we can have it not null, always put the
> description"

### 13. Requiring the deduplication pre-check

> "yes, add it"

Approving the change described under [Where I overrode it](#where-i-overrode-it).

### 14. A defect I found by running it

> "When I try to run the project: [404 problem+json]"

Running the service myself found something 56 tests could not. There was no route at `/`, because the
template's placeholder had been removed and never replaced, and the launch profile opens a browser
there. A reviewer would have hit exactly the same thing on their first run. The root now redirects to
the API document.

### 15. The video

> "draft the video running order"

### 16. Restructuring into layers

> "Can we have an application project that will have the managers folder with the
> WorkOrderServiceManager (for the business logic), the Model for the application model to map the
> domain model, also I feel like the the file should have one responsibility, if it's a model should
> have the table structure and not include the functionality, but the functionality should fall in
> the manager ... And instead of having the secrets in appsettings file, can we have the secrets in
> the secret file ... also the enums should have their own folder called Enumerations (And have the
> naming conversions like StatusChangeOutcomeEnum). And all the model properties, and the enums
> should have the descriptions ... In the API project we can have the requests and responses folders"

The most useful exchange in the project, because two parts of it were pushed back on and I took the
pushback.

**The enum suffix, dropped.** Microsoft's Framework Design Guidelines say explicitly not to suffix
enum type names with `Enum`. A team that lists SOLID and Clean Architecture is more likely to read
`StatusChangeOutcomeEnum` as unfamiliarity with the guidelines than as care. I kept the
`Enumerations` folder, which was the part that helped, and dropped the suffix.

**The anemic model, not taken.** I had asked for a data-only model with all the logic in the manager.
That is the common enterprise shape, and it would have cost the one property this design rests on:
`Status` would need a public setter, and "status and history can never diverge" would stop being
enforced by the type and become a convention every caller has to remember.

What I took instead gives me the layering without that loss: **the manager owns the use cases, the
entity keeps its own invariant.** Loading, the transaction boundary, deduplication and outcome
recording moved into `WorkOrderServiceManager`; the transition rule stayed on `WorkOrder.ApplyStatus`.
That is also what Clean Architecture prescribes, rather than a data bag plus a service.

**Three DTO shapes became two.** I had asked for application models and API responses as separate
types. With one transport there is nothing for the third shape to absorb, so API requests come in and
application models go out, and a new field is two edits rather than three.

The rest went in as asked: the `Application` project with `Managers`, `Models`, `Validations`,
`Abstractions` and `Enumerations`; a test project per production project; `Requests` and `Responses`
folders in the API; secrets moved to user secrets and a gitignored `.env`; and XML documentation on
every public member, with `GenerateDocumentationFile` on and warnings as errors so an undocumented
member fails the build.

Two consequences the plan had not anticipated, both forced by dependency direction. The validation
rules had to move to the application layer while the endpoint filter stayed in the API, because the
filter is an `IEndpointFilter` and putting it in the application project would have made that project
depend on ASP.NET Core, defeating the layering. And persistence had to move into the application
project rather than staying in the API, because the manager depends on the `DbContext` and the
reference would otherwise have been circular.

One payoff I had not expected: wiring the XML documentation into Swashbuckle means those descriptions
became the schema descriptions in the OpenAPI document, so what `occurredAt` means and why
`fromStatus` is nullable are readable in Swagger without opening the code.

## Where I overrode it

**`Description` was nullable; I made it required.** A work order always has one, and carrying a null
through to every response for no reason is worse than rejecting a blank.

**It proposed a transactional inbox, then withdrew it.** Persist the event on receipt, queue its id,
mark it done after processing. That is the right production answer to the durability gap. It withdrew
the suggestion once the four-hour guidance in the PDF was visible, and I agreed: it is scope the
brief did not ask for. It stays in SOLUTION.md as something considered and declined, which is more
useful than half of it in code.

**I required the deduplication pre-check it had left out.** Running the service showed every duplicate
producing an error-level log with a full stack trace, because duplicates were being found by letting
the insert fail. For an at-least-once source, redelivery is normal traffic, so normal traffic was
writing error logs. A read before the write keeps the common case off that path. The unique key stays,
because a read cannot close its own race. I wanted both, and the reasoning for both is in
SOLUTION.md.

## What it caught that I would have missed

Recorded plainly, because pretending otherwise would defeat the point of this document.

- **`DbContext` is scoped and a hosted service is a singleton**, so event handling needs its own
  scope. I know this. It is also the single most common way hosted services go wrong, and having it
  named up front meant it was right the first time rather than the second.
- **An unhandled exception in `ExecuteAsync` stops the hosted service.** The try/catch belongs around
  each item, not around the loop, or one bad event silently ends all processing for the life of the
  process.
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
   for speed" is the reflex answer and it is wrong for a service whose entire point is a background
   writer running alongside the API: the in-memory version was not testing the concurrency, it was
   failing on it. The tests now use a temporary SQLite file with WAL.

## What I verified by hand

Every claim in the README and SOLUTION.md was checked against a running service rather than inferred.

- The migration applied to a real SQL Server, including the clustered index placement, which only
  fails at apply time.
- The same `eventId` submitted three times: one status change, one history entry, one
  `ProcessedEvents` row.
- Queue exhaustion, with a capacity of one and forty events fired at once: sixteen accepted and
  twenty-four refused with 503 and `Retry-After`.
- The concurrency token, by driving two contexts at the same row: the stale writer gets a
  `DbUpdateConcurrencyException` and its write leaves nothing behind.
- `occurredAt` and `recordedAt` coming back as different values, which is the two-timestamp design
  visibly working.
- The generated OpenAPI document, which was wrong twice before it was right: enums documented as
  integers while the API emits strings, and a document-level security requirement claiming the open
  read endpoints need an API key.
- A clean clone from GitHub, built and fully tested with no setup at all.
