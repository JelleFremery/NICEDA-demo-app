# NICEDA Demo Plan

Working plan for the Techorama NL 2026 session **"Optimizing data for
agentic use"**. Built from a full read of `Optimizing data for agentic
use.pptx` (44 real content slides; slides 45–61 are hidden/empty
placeholders reserved for later demo screenshots) plus your clarifications
in chat. This file is the source of truth until the deck/plan changes —
update it alongside code as practices land.

## 1. Narrative & timing (≈50 min total)

| Deck anchor | Demo | Budget |
|---|---|---|
| Slide 10 ("NICEDA: Before") | **Before**: show NICEDA's raw CRM/SQL/Cosmos/SharePoint data — human-friendly, agent-hostile | ~5 min |
| Slide 40 ("NICEDA: After") | **After**: same data, reshaped per the 10 practices — semantic layer, metadata, classification, CQRS views | ~5 min |
| Slide 48 ("NICEDA: Agentic use") | **Agentic access**: one chat UI, switchable between Before/After datasets, using RAG + MCP + tools via Microsoft Agent Framework, showing the practices literally make-or-break retrieval/action quality | ~5 min |

Everything else is slide time; the app must start fast (Aspire/Docker,
pre-seeded data) and the three demo beats must each be reachable in one or
two clicks — no manual data entry live.

## 2. The fictional company

**NICEDA Inc.** ("Nothing Is Certain Except Data and Access") — enterprise
customer onboarding / identity integration / CRM sync / access governance
vendor, 15 years of accumulated data across 4 systems:

| System | Real or mocked | Contains | Owner | Classification |
|---|---|---|---|---|
| CRM | **Mocked** (seeded in-app, no real CRM product) | Customers | Sales | Confidential |
| Azure SQL | **Real** (SQL Server container via Aspire) | Contracts, Projects, Onboarding workflows | Finance | Restricted |
| Cosmos DB | **Real** (Cosmos DB emulator container via Aspire) | Support tickets, meeting notes, implementation docs, security assessments | Support | Confidential |
| SharePoint | **Mocked** (seeded folder/file structure, no real SharePoint) | PDFs, process docs, compliance reports | Legal | Restricted |

Per your description, each system's "Before" shape:
- **CRM**: optimized for human data entry; important content buried across
  dozens of loosely-typed fields.
- **SQL (Contracts)**: normalized for storage, not separated by use case
  (one join-heavy schema serving every purpose).
- **Cosmos (Support)**: inconsistent structure/meaning between documents,
  no metadata.
- **SharePoint (Legal)**: folder hierarchy as the only organizing
  principle, no metadata.

## 3. Before/After architecture

**Decision: duplicate data, one running app, UI toggle** (your chosen
option). Concretely:

- SQL Server container hosts one database, `niceda`, with `raw` and
  `optimized` schemas (one connection string).
- Cosmos DB emulator hosts one database with two support containers: raw vs
  optimized documents — optimized ones carry the metadata envelope from
  slide 7/24 (`customerId`, `classification`, `region`, `severity`, etc.)
  and are chunked by business concept (practice 7).
- Mocked CRM/SharePoint ship one seed dataset each, with thin optimized
  projections over those same records.
- The Web UI has a **Before/After toggle** (global, persists across the
  data-explorer and the chat) that swaps which dataset/connection the API
  and agent use. This lets the same chat question be asked twice live —
  "watch what happens when I flip this switch" is the money demo moment.

## 4. Solution layout — streamlined (fewer moving parts)

Simplicity is now a primary goal: the fewer projects/containers/concepts on
screen, the easier the *code* is to demo in 5-minute windows, not just the
app. Cut from the original plan: no standalone `NICEDA.Agent` project (the
agent orchestration lives directly in `NICEDA.ApiService`, where the data
already is — one fewer process to run/debug live), no separate database
per Before/After (one database, two schemas/containers — one connection
string, not two).

| Project | Role |
|---|---|
| `NICEDA.AppHost` | SQL Server + Cosmos DB emulator containers, wires connection strings/params into the other projects, orchestrates everything in Docker. |
| `NICEDA.ServiceDefaults` | Unchanged (telemetry/health/resilience). |
| `NICEDA.ApiService` | Owns all data access (mocked CRM/SharePoint + real SQL/Cosmos), the before/after toggle, the semantic/CQRS layer, the RAG pipeline, and the chat endpoint (Microsoft Agent Framework). One process = one thing to point at in the debugger. |
| `NICEDA.Web` | Blazor UI only: data explorer (4 systems + Before/After toggle), agentic chat panel, current-user selector. No business logic. |
| `NICEDA.McpServer` | Only the MCP-specific project, since separating RAG vs. MCP *is* a talking point. One real external MCP server exposing the governed write action(s). Everything else is in-process. |
| `NICEDA.Tests` | One xUnit project: practice-9 deterministic test set, plus a few fast unit tests added alongside each slice as it's built (not saved for the end). |

Six projects total (down from eight), one database connection per data
store, one agent host. `dotnet run` on `NICEDA.AppHost` is still the only
command needed to see the whole thing.

### Simplified Before/After data model

- **SQL**: one database, two schemas: `raw.*` (normalized, no business
  meaning) and `optimized.*` (views/semantic projections over the same
  underlying rows where sensible — duplicate only the handful of tables
  where the "before" shape must look structurally different, e.g. an
  over-normalized `raw.Customer`/`raw.Status`/`raw.Order` vs. an
  `optimized.CustomerHealthView`). Avoids keeping two copies of data in
  sync.
- **Cosmos**: one database, two containers: `raw-support` (inconsistent
  shape, no metadata) and `optimized-support` (metadata envelope, chunked
  by concept). Small, fixed seed set (5–10 documents) — enough to make the
  retrieval-quality difference obvious without bloating demo-day load time.
- **CRM/SharePoint**: single static seed file each (JSON), loaded into an
  in-memory repository at startup; the "before" shape *is* the mock (dozens
  of flat fields / folder paths), and the "after" shape is a thin projection
  computed over the same seed, not a second dataset.

## 5. Practice → code mapping (your final numbering)

Each practice gets a short code comment tagged `// NICEDA-PRACTICE-N: ...`
so they're greppable/bookmarkable during the live demo, plus a matching
entry in `docs/10-practices.md`. Source slide(s) noted for traceability.

| # | Practice | Deck source | Where it shows up in code |
|---|---|---|---|
| 1 | Build a data inventory | Slides 8/9/14 (Practice 1) | `docs/10-practices.md` catalog + a literal `NICEDA.DataInventory` (system/data/owner/classification/agent-potential table) that the API and seed code reference instead of hardcoding, so it doubles as living documentation. |
| 2 | Identify agent use cases | Slide 20 (Practice 2) | A short `docs/agent-use-cases.md` (e.g. "identify customers at risk") driving which data the semantic layer/MCP tools actually expose — written before the schema, not after. |
| 3 | Add a semantic layer | Slides 21–26 (Practice 3) | `NICEDA.ApiService`'s "after" views (`CustomerHealthView`-style projections) + a canonical-term/alias glossary, replacing raw joins the agent would otherwise have to reverse-engineer. |
| 4 | Classify during ingestion | Slides 27–29 (Practice 4) | Seed/ingestion pipeline stamps `classification`/`owner`/`retention`/`region` at write time, never bolted on later. |
| 5 | Separate authorization and retrieval | Slides 32/33 (Practice 5) | Security context (current-user selector) resolved **before** the vector/metadata filter is applied; security fields travel with the chunk but are never embedded. |
| 6 | Design for deletion | Slide 34 (Practice 6) | A `DeleteCustomerData` command that fans out across SQL rows, Cosmos docs, and the vector index — demonstrates the GDPR "is it actually deleted everywhere" problem. |
| 7 | Chunk based on meaning | Slides 35–37 (Practice 7) | Optimized Cosmos documents pre-chunked by concept (onboarding, security review, escalation) instead of fixed-size windows; raw dataset intentionally keeps the "50-page blob" shape for contrast. |
| 8 | Apply the CQRS pattern | Slide 39 (Practice 8) | `NICEDA.ApiService` read models (`GetCustomerHealth`, `GetRenewalRisk`) vs. write commands (`CreateFollowUpTaskCommand`) — no raw `SELECT *`/table exposure to the agent. |
| 9 | Verify the use case with automated deterministic tests | Slide 42 (Practice 9) | `NICEDA.Tests`: fixed question → expected-facts assertions against the RAG/agent pipeline, runnable via `dotnet test`. |
| 10 | Close the loop (create feedback data) | Slide 43 (Practice 10) | Agent tool outcomes, failures, and user interactions get written back as structured "feedback" records (new Cosmos documents) linked to the originating use case/semantic entity, accumulating over time for future eval/retrieval. The SQL `audit.AgentActionLog` adds the per-tool-call trail. |

Relations (slides 30/31, "Designing data for retrieval") aren't a separate
numbered practice — they're folded into practices 1 and 3 (the inventory
and semantic layer already capture customer→contract→ticket relationships).

## 6. Agentic layer

- **Model**: Azure AI Foundry endpoint, chat model `gpt-6.1-sol`,
  embeddings model `text-embedding-3-small`. Credentials go into user
  secrets / environment variables wired through Aspire — **never
  committed**. (You pasted a live key in chat; treat it as needing
  rotation once it's out of a chat transcript.)
- **Orchestration**: Microsoft Agent Framework in `NICEDA.ApiService`.
- **RAG**: in-memory vector index (no extra container), built from the
  optimized Cosmos documents' chunks + embeddings; security filter applied
  before semantic search per practice 5.
- **Tools (kept to a minimal, demoable set)**:
  1. `search_knowledge` — in-process RAG tool (vector search over the
     optimized Cosmos chunks, security-filtered).
  2. `get_customer_health` — in-process CQRS read tool (practice 8),
     available only in After; Before explicitly lacks this capability.
  3. `create_follow_up_task` — the one real MCP tool, served by
     `NICEDA.McpServer`, kept logically and physically separate from the
     in-process tools per your instruction to isolate MCP logic.
  Three tools is enough to show "RAG for knowledge, MCP for capability"
  (slide 38) without turning the chat demo into a tool-sprawl tour.
- **Identity**: a current-user dropdown ("Jane — CustomerSuccess,
  EU, Betabit+Fenetre+Techorama" vs "Admin") drives the security context for both
  RAG filtering and MCP tool authorization — no real sign-in required for
  the demo.
- **Runs against both datasets**: the same agent/chat endpoint takes the
  Before/After toggle as context so you can ask the identical question
  twice live and show the retrieval/action quality difference.

## 7. Operational caveats

- **Cosmos DB emulator in Docker**: the Linux Cosmos DB emulator container
  is heavier and slower to start than SQL Server; first boot can take a
  couple of minutes. Fine for prep, but don't rely on a cold start during
  the live 50-minute slot — start Docker ahead of time.
- **Microsoft Agent Framework** and the HTTP MCP transport are implemented
  with pinned package versions and deterministic/transport checks.
- **Foundry key handling**: configured through `dotnet user-secrets` /
  secret Aspire parameters, never committed or baked into Compose output.
  Rotate the exposed key before use. Live generation remains unverified
  until a valid credential and the configured deployments are available.
- Practice 10's shape is based on your chat clarification, not deck text —
  flagging in case you want to adjust the wording once it's actually in
  the slides.

## 8. Engineering workflow (applies to every step below)

- **Warnings as errors**: `Directory.Build.props` sets
  `TreatWarningsAsErrors` + `AnalysisLevel` for the whole solution. A build
  with warnings is a failed build — fix it before moving on, don't
  suppress it.
- **Tests land with the code, not after**: each slice in the sequence
  below adds its own test(s) in `NICEDA.Tests` in the same step, then runs
  `dotnet build` and `dotnet test` before moving to the next slice.
  Practice 9 (deterministic test sets) isn't a final step — it's the habit
  the whole build follows.
- **Fast test default**: normal `dotnet test` runs deterministic tests
  without Docker or Foundry. Docker checks explicitly opt in against an
  already-running AppHost; they do not cold-start/tear down containers per
  run. `NICEDA.Tests/test.runsettings` imposes a four-minute run deadline,
  and HTTP requests time out after 20 seconds. Skipped Docker checks are
  reported explicitly, not counted as passed.
- **Small commits**: one slice = one (or a few) commits, so the repo
  history narrates before → after → agentic, and so any step can be
  demoed/diffed in isolation if the talk runs long on Q&A.
- **Simplicity over completeness**: when a choice appears between "more
  realistic" and "fewer moving parts to explain live", pick fewer moving
  parts. This is a 5-minute-per-beat demo, not a production system.

## 9. Build sequence (small, demoable, test-covered commits)

1. Repo scaffolding: `Directory.Build.props` (warnings-as-errors), updated
   `AGENTS.md`/`copilot-instructions.md`, `NICEDA.Tests` project with a
   trivial smoke test — proves the workflow before any feature code.
2. Data inventory + fictional company seed data (mocked CRM/SharePoint,
   raw SQL schema, raw Cosmos container) — "Before" state fully running,
   data-explorer UI only, no agent yet. Tests: seed data shape/counts.
3. Semantic/optimized layer (SQL views, optimized Cosmos container, CQRS
   read/write) — "After" state, same UI toggle. Tests: optimized views
   return expected business fields; CQRS command round-trips.
4. RAG pipeline (embeddings + in-memory index + security-aware filtering).
   Tests: security filter excludes out-of-scope docs; a fixed question
   returns the expected chunk.
5. MCP server + the two in-process tools + Agent Framework chat endpoint +
   Web chat UI. Tests: MCP tool contract test; chat endpoint smoke test.
6. Deterministic test set (practice 9, formalizes step 4/5's ad-hoc tests
   into the named practice) + feedback-loop write-back (practice 10).
   Tests: feedback record is created and linked after a tool call.

Each step gets its own commit(s) and a `docs/10-practices.md` update, so
the repo history itself narrates the before→after→agentic arc for the
talk.

## 10. Implementation status

All six slices are implemented: Before/After explorer, semantic/CQRS
models, security-aware RAG, separate HTTP MCP service, Agent Framework
chat, deterministic checks, deletion, and durable Cosmos feedback.
Betabit, Fenetre, and Techorama are included in the fictional seed.

Both host-service Aspire execution and all-service Linux Docker Compose
execution have been exercised against real SQL and Cosmos. Warm-container
integration checks finish in seconds; normal tests are offline, with
explicit integration skips and a four-minute overall deadline. The
remaining external dependency for a full live chat demo is a rotated
Foundry credential and access to both configured model deployments.

The code-review fixes are applied: source authorization precedes
embedding as well as ranking, Before no longer uses the optimized health
tool, task retries have durable idempotency, feedback failure does not
hide a completed action, and startup reconciles partial deletions through
the full lifecycle. See `code-review-fix-plan.md` for regression coverage
and `demo-speaker-notes.md` for the updated presenter script.
