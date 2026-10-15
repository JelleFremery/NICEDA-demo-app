# NICEDA demo briefing

Read this before the session. The stage script itself (paste-ready
speaker notes for slides 10, 40 and 48) is in `docs/demo-script.md`.

- **Part 1 — Refresher:** which customer proves what, the proof map,
  prompts, prep checklist, evidence checkpoints, fallback, rehearsal.
- **Part 2 — Code tour:** Visual Studio bookmarks B01–B21.
- **Part 3 — Background:** fictional company, architecture, solution
  layout, practice-to-code mapping, agentic layer, engineering workflow,
  build sequence. Other docs refer to these as §1–§10.

The deck's practice numbers match `docs/10-practices.md` 1–10. If the
deck has been reordered, use the slide titles: "NICEDA: Before" (10),
"NICEDA: After" (40), "NICEDA: Agentic use" (48). Each demo is five
minutes in total.

## Part 1 — Refresher
### The cast: which customer proves what

Do not tour every customer. Each one has a single job in the story:

| Customer | Role in the story | Where it pays off |
|---|---|---|
| **Betabit** (Jane's, EU) | **Is this account at risk?** CRM status says "Active, watch closely", while the sales notes say the champion left and there is a competing quote. SQL says `AtRisk`. Support has the same customer in two document shapes. | Before: scattered meaning. After: one profile, view `RiskLevel = High`, one chunk envelope. |
| **Contoso** (NA, admin only) | **The stage question.** "Summarize Contoso's security review with sources." The answer sits in one raw assessment blob (`support-005`), which the After state turns into three concept chunks (`optimized-sup-005a/b/c`). | Every beat. Also the permission boundary for Jane. |
| **Fabrikam** (NA) | **Permissions come from policy, not text.** CRM lists Jane Okafor as account manager, yet Jane never sees Fabrikam: her scope is an explicit EU customer list (B13). | Before, last beat. Backup risk question. |
| **Techorama** (Jane's, EU) | **"Never touched"** in Agent usage. Only reliable on a fresh seed: broad search results can touch it. | Agentic, last beat (with caveat). |
| **Fenetre** (Jane's, EU) | **Healthy control.** Renewed, ~60k, view `RiskLevel = Low`. Shown next to Betabit, it proves the risk rule and the agent tell accounts apart instead of flagging everyone. | After SQL beat; Jane's risk question (backup). |

### Proof map: concept → evidence → bookmark

| Practice | What the audience sees | Prompt / action | Code proof |
|---|---|---|---|
| 1 Inventory | Four systems, owners, classification | Before > any tab | B01 |
| 2 Use cases | Three questions plus one action drive the design | (slide) | B02 |
| 3 Curated concepts | Betabit: dozens of CRM fields → nine-field profile with `SourceSystem`/`SourceId` | After > CRM flip | B15 |
| 4 Classify at ingestion | Betabit legal file and Contoso chunks carry classification, owner, retention | After > SharePoint / Cosmos flip | B05, B16 |
| 5 Security before ranking | Jane: no Contoso evidence, nothing about Contoso embedded | Jane + hero prompt | B06, B17, B13 |
| 6 Lifecycle | Deletion removes vectors, feedback, audit rows | (say, do not run) | B07 |
| 7 Chunk by meaning | Before: 60-char fragments, top 2. After: 005a/b/c citations | Hero prompt Before vs After | B14, B05 |
| 8 CQRS / capabilities | Health view answers risk; task is a validated command; health tool is After-only | Hero prompt + task prompt | B04, B19, B10, B11 |
| 9 Deterministic tests | The stage question is a unit test on the real seed | (show test) | B18 (B08 backup) |
| 10 Feedback + audit | Feedback in Cosmos; every tool call in `audit.AgentActionLog`; Agent usage roll-up | Feedback buttons, Audit log, Agent usage | B12, B20, B21 |

### Before the audience arrives (not part of the demo clock)

1. Rotate the previously shared Foundry key, then configure AppHost user
   secrets as described in `README.md`. Confirm that both the chat and
   embedding deployments are available.
2. Start Docker. Build `NICEDA.sln` before launching the AppHost, then run
   `dotnet run --project NICEDA.AppHost --no-build`. Wait for seeding to
   complete. Do not cold-start the Cosmos emulator on stage.
3. Open the Web link from the Aspire dashboard. Check all six explorer
   tabs (CRM, SQL, Cosmos DB, SharePoint, Audit log, Agent usage) in
   Before and After. As **admin**, confirm all five customers appear.
   As **Jane**, confirm only Betabit, Fenetre and Techorama appear.
4. **Warm-up, and its side effect.** Open Chat as **admin** and submit the
   hero prompt (below) once in Before and once in After. This verifies
   model access and warms both embedding indexes. Inspect the real
   answers, citations and traces; do not assume Before will always be
   worse. Every search hit writes an audit row, so warming can mark
   Techorama as "touched" in Agent usage. Look at Agent usage now and
   decide which zero cell you will point to (see Agentic, last beat).
5. Write the follow-up due date into your notes:
   `(Get-Date).ToUniversalTime().AddDays(14).ToString('yyyy-MM-dd')`.
   Task creation changes data: do not repeat it just to warm the model,
   and do not delete a seeded customer to reset the demo.
6. Run the normal tests (`dotnet test NICEDA.Tests`). For live checks,
   follow the opt-in variables in `README.md` and use `--no-build` while
   the AppHost is running.
7. In the open NICEDA Visual Studio solution, run
   `.\tools\Set-DemoBookmarks.ps1` from Developer PowerShell. Open
   **View > Bookmark Window** (`Ctrl+K, Ctrl+W`). Pre-open the files for
   B01, B04, B05, B06, B14, B17, B18, B19, B10 and B20 as tabs.
8. Set a readable font size. Keep the browser, Visual Studio and the
   presentation adjacent. Start on **Data explorer > Before > CRM**, with
   **admin** selected.

Do not show the dashboard's connection-string environment or user secrets
on the projector. The identity dropdown is a demo security context, not
authentication.

#### The prompts (also in `docs/demo-script.md`, slide 48)

- **Hero prompt** (slide 47's workflow in one sentence):
  `Summarize Contoso's security review with sources, check Contoso's current account health, and recommend one follow-up action.`
- **Task prompt** (complete on its own; each chat submission is a new run
  with no memory of the previous answer):
  `Create one follow-up task for Contoso. Reason: Techorama live demo - add a dedicated Contoso escalation contact. Due date: [YYYY-MM-DD].`
- **Backup, Jane:** `Which of my customers are at risk and why?`
  (After: Betabit High with reasons; Fenetre Low as the healthy control —
  "it didn't cry wolf"; no Fabrikam.)
- **Backup, Jane:** the task prompt above. The result is an explicit MCP
  authorization failure and a failed audit row.
- **Backup, admin:** `What are Contoso's open risks?` (slide 42's example).


### Evidence checkpoints (slide 48)

- Before (admin) retrieval returns two concept-less fragments; they may or
  may not come from `support-005`.
- After can cite `optimized-sup-005a/b/c`, all traceable to `support-005`.
  The exact wording and subset of citations depend on the model, so don't
  promise all three every time.
- Jane must never receive Contoso sources; her `get_customer_health` call
  for Contoso is logged as denied.
- Agent usage zero cells:
  - `create_follow_up_task` for Fenetre/Techorama is always reliable.
  - "Techorama never touched" holds only on a fresh seed with no broad
    search (see the prep checklist, step 4).

**Important comparison boundary:** the hero prompt compares *both* data
shape and capability. Before has no health tool by design. To isolate
retrieval quality only, use "Summarize Contoso's security review with
sources." on its own. An empty or denied answer for Jane does not mean
After retrieval is broken; it is the permission boundary working.


### Deletion detail, if asked (slide 40)

At startup, durable deletion markers are
retried through the full lifecycle before seeding. The real-store
regression test uses a scratch customer, fails after SQL commits, then
verifies restart reconciliation across original and derivative data. Do
not delete a seeded customer live.

### Honest fallback (all three demos)

If a service fails, show the explicit error and name the missing
dependency. Do not restart Docker or troubleshoot credentials on stage.
Use the prepared code tabs and the latest successful test results instead
(B18 shows the stage question passing offline). If you capture
screenshots during rehearsal, label them as rehearsal captures, not live
output.

Missing Foundry credentials intentionally return an unavailable response,
not a simulated answer. Feedback must be submitted before the API
restarts, because interaction lookup is in memory. Feedback that was
already written stays durable in Cosmos.

Completed actions appear with their task IDs even if outcome-feedback
persistence fails; a warning accompanies them. Retrying the same pending
request in the same chat view reuses its idempotency key, so SQL returns
the existing task or rejects a changed payload. A new request gets a new
key and represents a new action. After an uncertain network outcome, do
not reload, navigate away, or blindly repeat a task: check its recorded
ID first.

### Rehearsal verification

**Current baseline:**
- The solution builds with zero warnings.
- The offline suite runs the stage question (B18) against the real seed.
- All 21 code-tour bookmark anchors must resolve uniquely
  (`DemoRehearsalTests`).

**Live rehearsal (13 October 2026; earlier test and bookmark counts)**

Data explorer:
- All four systems loaded in both modes for both identities.
- Jane's records excluded Contoso and Fabrikam.
- Admin's After support view showed all three Contoso security chunks.

Model and API:
- Live validation found that the default reasoning deployment rejects
  tools through Chat Completions. Agent Framework therefore uses the
  Responses adapter, with an approved, narrowly scoped experimental SDK
  opt-in. No fake model or embedding fallback was used.

Chat:
- Before (admin) cited `support-005`.
- After (admin) cited all three `optimized-sup-005a/b/c` concepts.
- Jane reported no accessible Contoso review and got only authorized EU
  sources.
- Each summary request took about 9–11 seconds.

Follow-up task and feedback:
- The browser created Contoso task **2015**. It was verified against the
  SQL read endpoint.
- Feedback was verified in Cosmos.
- Both remain persisted; do not repeat the request to reset the demo.

Risk questions:
- The After risk question invoked `get_customer_health` and returned
  Jane's three accounts, with Betabit at high risk.
- A Before task request failed and left the task count unchanged.

These outcomes validate capabilities and evidence. They do not guarantee
identical wording on the day.

## Part 2 — Code tour (Visual Studio bookmarks)

The installer adds **actual native Visual Studio bookmarks** to the open
NICEDA solution, without removing existing bookmarks or changing source
files. It resolves unique text anchors, so line movement does not silently
send the code tour to the wrong place.

From **Windows PowerShell 5.1**, with `NICEDA.sln` open:

```powershell
.\tools\Set-DemoBookmarks.ps1
```

For Visual Studio 2022:

```powershell
.\tools\Set-DemoBookmarks.ps1 -ProgId VisualStudio.DTE.17.0
```

The default targets the installed Visual Studio 2026 (`18.0`). It does
not launch an IDE or switch a different open solution.

Open **View > Bookmark Window**, normally `Ctrl+K, Ctrl+W`. Next/previous
bookmark are normally `Ctrl+K, Ctrl+N` / `Ctrl+K, Ctrl+P`. Keyboard
bindings can differ by profile. The installer assigns the native names
**B01 - Inventory before architecture**, **B02 - Three use cases before
schemas**, and so on using `tools\demo-bookmarks.json`. It also removes
duplicate demo markers from the original installer. The Bookmark Window
is searchable/scannable by these descriptive names; grouping into folders
remains optional.

| Label | Slide/demo | File | Show this point |
|---|---|---|---|
| B01 | 10, Before | `NICEDA.ApiService\DataInventory\DataInventoryCatalog.cs` | Inventory ownership, classification, agent potential. |
| B02 | 10, Before | `docs\agent-use-cases.md` | Questions and action define the data requirements. |
| B03 | 10, Before | `NICEDA.ApiService\Sql\RawSqlSeeder.cs` | Normalized tables and raw status codes. |
| B04 | 40, After | `NICEDA.ApiService\Sql\OptimizedSqlSeeder.cs` | Business-named view over the original SQL rows. |
| B05 | 40, After | `NICEDA.ApiService\SupportDocs\OptimizedSupportSeeder.cs` | Metadata envelope and concept-based chunks. |
| B06 | 40, After | `NICEDA.ApiService\Rag\KnowledgeSearchService.cs` | Identity filters sources before embedding, and candidates before ranking. Scope-specific indexes prevent admin cache reuse for Jane. |
| B07 | 40, After | `NICEDA.ApiService\DataLifecycle\CustomerDataLifecycle.cs` | Mocks/vectors, original stores, feedback participate in deletion; startup reconciles durable markers. |
| B08 | 40, After | `NICEDA.Tests\RagKnowledgeSearchTests.cs` | Fixed expected source, not an LLM judging another LLM. |
| B09 | 48, Agentic | `NICEDA.ApiService\Agent\AgentChatService.cs` | Business read tool beside search and MCP orchestration; optimized health is an After-only capability. |
| B10 | 48, Agentic | `NICEDA.McpServer\Tools\CreateFollowUpTaskTool.cs` | Separate MCP command; request-context identity, not an identity argument. |
| B11 | 48, Agentic | `NICEDA.ApiService\Sql\FollowUpTaskCommands.cs` | Named, validated write capability, not arbitrary SQL. |
| B12 | 48, Agentic | `NICEDA.ApiService\Agent\AgentChatService.cs` | Feedback linked to interaction/customer. Open companion `AgentChatOutcomeRecorder.cs` to show how persistence failures warn without hiding confirmed actions. |
| B13 | backup, permission question | `NICEDA.ApiService\Security\DemoIdentity.cs` | Jane's explicit Betabit/Fenetre/Techorama scope and EU region policy (why Fabrikam stays hidden although CRM names Jane as account manager). |
| B14 | 10, Before / 48 contrast | `NICEDA.ApiService\Rag\KnowledgeSearchService.cs` | Naive RAG: 60-char fragments, concept dropped, top 2. Explains why Before chat answers miss the Contoso findings. |
| B15 | 40, After | `NICEDA.ApiService\Crm\CustomerProfile.cs` | Nine curated fields, `HealthSummary`, `SourceSystem`/`SourceId` provenance. |
| B16 | 40, After | `NICEDA.ApiService\Legal\LegalKnowledge.cs` | SharePoint path becomes concept, classification, owner, retention. |
| B17 | 40, After | `NICEDA.ApiService\Rag\CosmosKnowledgeDocumentSource.cs` | Region/classification/owner/retention filter the chunk but never enter the embedding. |
| B18 | 40, After (alt. to B08) | `NICEDA.Tests\RagKnowledgeSearchTests.cs` | The exact stage question against the real seed: After cites 005a/b/c, Before gets concept-less fragments, Jane never embeds Contoso. |
| B19 | 48, Agentic | `NICEDA.ApiService\Agent\AgentChatService.cs` | `get_customer_health` is registered only in After mode. |
| B20 | 48, Agentic | `NICEDA.ApiService\Agent\AgentChatService.cs` | Every tool call (including denials) writes `audit.AgentActionLog`. |
| B21 | 48, Agentic | `NICEDA.ApiService\Audit\AgentUsage.cs` | Agent usage summary: which customers/capabilities the agent touched, and which it never did. |

Run with an unlocked interactive desktop and the IDE available to receive
rename input. The script verifies native names and that **Next Bookmark**
can reach every inserted location. Re-running it is idempotent. All targets/labels are in
`tools\demo-bookmarks.json`.

Visual Studio owns persistence in its local solution state (normally
`.vs`/`.suo`, ignored by Git). Close the solution/IDE normally when you
want it to save that state; the installer does not close your session or
save unsaved source edits. Use the committed manifest/installer to
recreate bookmarks on another machine.

## Part 3 — Background: architecture and build plan

Working plan for the Techorama NL 2026 session **"Optimizing data for
agentic use"**. Built from a full read of `Optimizing data for agentic
use.pptx` (44 real content slides; slides 45–61 are hidden/empty
placeholders reserved for later demo screenshots) plus your clarifications
in chat. This part is the source of truth for architecture until the
deck/plan changes — update it alongside code as practices land.

### 1. Narrative & timing (≈50 min total)

| Deck anchor | Demo | Budget |
|---|---|---|
| Slide 10 ("NICEDA: Before") | **Before**: show NICEDA's raw CRM/SQL/Cosmos/SharePoint data — human-friendly, agent-hostile | ~5 min |
| Slide 40 ("NICEDA: After") | **After**: same data, reshaped per the 10 practices — semantic layer, metadata, classification, CQRS views | ~5 min |
| Slide 48 ("NICEDA: Agentic use") | **Agentic access**: one chat UI, switchable between Before/After datasets, using RAG + MCP + tools via Microsoft Agent Framework, showing the practices literally make-or-break retrieval/action quality | ~5 min |

Everything else is slide time; the app must start fast (Aspire/Docker,
pre-seeded data) and the three demo beats must each be reachable in one or
two clicks — no manual data entry live.

### 2. The fictional company

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

### 3. Before/After architecture

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

### 4. Solution layout — streamlined (fewer moving parts)

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

#### Simplified Before/After data model

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

### 5. Practice → code mapping (your final numbering)

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
| 10 | Close the loop (create feedback data) | Slide 43 (Practice 10) | Agent tool outcomes, failures, and user interactions get written back as structured "feedback" records (new Cosmos documents) linked to the originating use case/semantic entity, accumulating over time for future eval/retrieval. The SQL `audit.AgentActionLog` adds the per-tool-call trail, and the "Agent usage" tab (`Audit/AgentUsage.cs`) rolls it up to answer "which data is never touched?". |

Relations (slides 30/31, "Designing data for retrieval") aren't a separate
numbered practice — they're folded into practices 1 and 3 (the inventory
and semantic layer already capture customer→contract→ticket relationships).

### 6. Agentic layer

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

### 7. Operational caveats

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

### 8. Engineering workflow (applies to every step below)

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

### 9. Build sequence (small, demoable, test-covered commits)

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

### 10. Implementation status

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
and `demo-script.md` for the presenter script.
