# AI usage

I used Claude (Claude Code) throughout this assignment. This is what I asked it, what I did with the
answers, and where my judgement and its suggestions differed.

## How I worked with it

I set the arrangement at the start: I make the design decisions, it reviews and challenges them.
The reason is practical rather than principled. This repository is the basis of a technical
interview, so nothing can ship that I cannot explain, and code I did not reason about would be a
liability rather than a saving.

Partway through I changed how the code got written. Reviewing is where I am fastest, so I moved to
having it produce each piece and reviewing every one before moving on. The constraint did not
change: each step was read, questioned and in several cases sent back.

Deliberately, I wrote my own plain-English reading of the brief **before** giving it the assignment
PDF, so I would find out where my understanding was wrong rather than have it filled in for me.

## The prompts, in order

Quoted as typed, including the untidy ones. A cleaned-up log would not be the thing that was asked
for.

**1. My own breakdown of the assignment, with the instruction to coach rather than build**

> "Plain-English breakdown of the assignment (so we're aligned before building): ... How I want to
> work: I'll face a technical interview that deep-dives this exact project, so coach me and review
> my code, but let me build it and understand every decision. Let's plan the architecture and data
> model first — I'll propose approaches, you refine."

It confirmed the breakdown and flagged two things I had under-weighted: concurrency between the API
and the worker, and the fact that an in-memory queue silently loses accepted events on restart. It
also reframed my list of decisions in a way that changed my thinking. I had been treating the set of
legal transitions as the interesting question. The better question is *where the rule is enforced*,
because there are two entry points and they must not each hold a copy.

**2. The assignment PDF, with no accompanying instruction**

Checking my reading against the source caught things I had missed: that Minimal APIs are mandatory
and therefore that request validation has to be added deliberately, and that the wording is
"multiple effects" rather than "multiple status changes", so a duplicate must not append a second
history entry either. That last one changed a test: asserting only on status would pass even with
duplicated history.

**3. The recruiter's email, pasted in full**

It carries requirements the PDF does not, including this document. It also moved the timebox from
about four hours to seventy-two, which changed what was worth building.

**4.**

> "I hope we will be able to review the AI_USAGE.md and correct the structure when we finish the
> project."

The answer that mattered was that the capture could not wait even if the editing could, so a raw
working log was started at that point and this document was edited down from it at the end.

**5.**

> "Can we also use the ejm ai project to assist us in architecture of the api?"

I asked whether I could take architectural cues from a production .NET codebase I work on. It looked
at it and advised against: that codebase uses Dapper and stored procedures with no EF Core, and
controllers rather than Minimal APIs, so the parts most worth copying are exactly the parts that
contradict this brief.

**6.**

> "If the Ejm is misleading, please lets not use it as a reference."

Dropped entirely. A reference you have to keep correcting is not a reference. It is also my
employer's code and this repository is public.

**7.**

> "start the raw AI usage log now,
>
> And we can start building our project here: "C:\Users\...\source\repos""

**8.**

> "give me your recommended answers for the four decisions"

The four I had left open: the transition set and its enforcement point, stored versus derived
status, the deduplication approach and its transaction boundary, and one consumer or many. I asked
for its recommendations, reviewed each, and adopted all four.

**9.**

> "I'm happy with your decision, you corrected lot of things I could have went wrong on"

Adopting all four is the honest record, so it is written down. What I took from it is not the
conclusions but the arguments, and those are in SOLUTION.md in my own words, because those are what
I have to defend. The one that changed my thinking most is the invariant framing: the thing being
protected is not "transitions are legal" but "status and history can never diverge", which is what
puts the history append inside the same method as the status assignment.

**10.**

> "you scaffold it and I'll review each piece (The thing is that if I write myself it's going to
> take time, it's not like I don't know coding, I'll review the code and give my judgement from
> there, you know I've been doing this in other project, reviewing is what I'm good at)
>
> And with regards to the from and to status, since the history is acting as an Audit trail, so that
> will be a good idea to have."

The change in working mode, and a decision of my own: history records both `FromStatus` and
`ToStatus`, because an audit trail you cannot check for gaps is not much of one.

**11.**

> "continue with step 2" ... "continue with step 7"

Each step produced code, which I read before moving on.

**12.**

> "With regards to the description being nullable, I think we can have it not null, always put the
> description"

**13.**

> "yes, add it"

Approving the deduplication pre-check described below.

**14.**

> "When I try to run the project: [404 problem+json]"

Running it myself found something the tests could not: there was no route at `/`, because the
template's placeholder had been removed and never replaced, and the launch profile opens a browser
there. A reviewer would have hit exactly the same thing. The root now redirects to the API document.

**15.**

> "draft the video running order"

**16.**

> "Can we have an application project that will have the managers folder with the
> WorkOrderServiceManager (for the business logic), the Model for the application model to map the
> domain model, also I feel like the the file should have one responsibility, if it's a model should
> have the table structure and not include the functionality, but the functionality should fall in
> the manager ... And instead of having the secrets in appsettings file, can we have the secrets in
> the secret file ... also the enums should have their own folder called Enumerations (And have the
> naming conversions like StatusChangeOutcomeEnum). And all the model properties, and the enums
> should have the descriptions ... In the API project we can have the requests and responses folders"

A restructure late in the build, and the most useful exchange in the project, because two parts of it
were pushed back on and I took the pushback.

**The enum suffix I dropped.** Microsoft's Framework Design Guidelines say explicitly not to suffix
enum type names with `Enum`. A team that lists SOLID and Clean Architecture is more likely to read
`StatusChangeOutcomeEnum` as unfamiliarity with the guidelines than as care. I kept the
`Enumerations` folder, which was the part that actually helped, and dropped the suffix.

**The anemic model I did not take.** I had asked for a data-only model with all the logic in the
manager. That is the common enterprise shape, and it would have cost the one property this whole
design rests on: `Status` would need a public setter, and "status and history can never diverge"
would stop being enforced by the type and become a convention that every caller has to remember.

What I took instead is the split that gives me the layering without that loss: **the manager owns
the use cases, the entity keeps its own invariant.** Loading, the transaction boundary,
deduplication and outcome recording all moved into `WorkOrderServiceManager`; the transition rule
stayed on `WorkOrder.ApplyStatus`. That is also what Clean Architecture actually prescribes, rather
than a data bag plus a service.

**Three shapes became two.** I had asked for application models and API responses as separate
types. With one transport there is nothing for the third shape to absorb, so API requests come in
and application models go out, and a new field is two edits rather than three.

The rest went in as asked: the `Application` project with `Managers`, `Models`, `Validations`,
`Abstractions` and `Enumerations`; a test project per production project; `Requests` and `Responses`
folders in the API; secrets out of `appsettings.json` and into user secrets and a gitignored `.env`;
and XML documentation on every public member, with `GenerateDocumentationFile` on and warnings as
errors so an undocumented member fails the build.

Two things I had not anticipated came out of it. The validation rules had to move to the application
layer while the endpoint filter stayed in the API, because the filter is an `IEndpointFilter` and
putting it in the application project would have made that project depend on ASP.NET Core, defeating
the layering. And persistence had to move into the application project rather than staying in the
API, because the manager depends on the `DbContext` and the reference would otherwise be circular.

The payoff I did not expect: wiring the XML documentation into Swashbuckle means those descriptions
are now the schema descriptions in the OpenAPI document, so what `occurredAt` means and why
`fromStatus` is nullable are readable in Swagger without opening the code.

## Where I overrode it

**Description was nullable; I made it required.** A work order always has one, and carrying a null
through to every response for no reason is worse than rejecting a blank.

**It proposed a transactional inbox, then withdrew it.** Persist the event on receipt, queue its id,
mark it done after processing. That is the right production answer to the durability gap. It
withdrew the suggestion once the four-hour guidance in the PDF was visible, and I agreed: it is
scope the brief did not ask for. It stays in SOLUTION.md as something I considered and declined,
which is more useful than half of it in code.

**I asked for the deduplication pre-check that had been left out.** Running the service showed every
duplicate producing an error-level log with a full stack trace, because duplicates were being
discovered by letting the insert fail. For an at-least-once source, redelivery is normal traffic, so
normal traffic was writing error logs. Adding a read before the write keeps the common case off that
path. The unique key stays, because a read cannot close its own race. I wanted both, and the
reasoning for both is in SOLUTION.md.

## What it caught that I would have missed

Recorded plainly, because pretending otherwise would defeat the point of this document.

- **`DbContext` is scoped and a hosted service is a singleton**, so event handling needs its own
  scope. I know this; it is also the single most common way hosted services go wrong, and having it
  named up front meant it was right the first time.
- **An unhandled exception in `ExecuteAsync` stops the hosted service.** The try/catch belongs around
  each item, not around the loop, or one bad event silently ends all processing.
- **`NuGet` restore failed with a 401** because this machine has a private feed configured. It would
  have failed the same way on a clean clone and looked like a broken repository. Fixed with a
  repository-level `NuGet.config`.

## What the tests caught, which is the part I would not have predicted

Ten of the seventeen integration tests failed on the first run, for three separate reasons, and all
three were real:

1. **SQLite refuses to `ORDER BY` a `DateTimeOffset`.** This broke every read that orders history or
   lists work orders, including a plain 404 lookup, because it fails at query translation before any
   rows are involved.
2. **A `Guid[]` with `Contains` in an EF query** binds to the `ReadOnlySpan` overload and fails as an
   unreadable type-load error. A `List<Guid>` binds correctly.
3. **One `SqliteConnection` shared between the request path and the background worker** produced
   `database is locked`. This is the one I would raise unprompted in an interview. "SQLite in memory
   for speed" is the reflex answer and it is wrong for a service whose entire point is a background
   writer running alongside the API. The in-memory version was not testing the concurrency, it was
   failing on it. The tests now use a temporary SQLite file with WAL.

## What I verified myself

Every claim in the README and SOLUTION.md was checked against a running service rather than
inferred:

- The migration applied to a real SQL Server, including the clustered index placement, which only
  fails at apply time.
- The same `eventId` submitted three times: one status change, one history entry, one
  `ProcessedEvents` row.
- `occurredAt` and `recordedAt` coming back as different values, which is the two-timestamp design
  visibly working.
- The generated OpenAPI document, which was wrong twice before it was right: enums documented as
  integers while the API emits strings, and a document-level security requirement claiming the open
  read endpoints need an API key.
