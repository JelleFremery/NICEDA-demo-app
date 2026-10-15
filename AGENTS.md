# AGENTS.md

Guidance for any coding agent (GitHub Copilot, or otherwise) working in
this repository.

## What this repo is

NICEDA is the demo solution for the Techorama 2026 talk
[**Optimizing data for agentic use**](https://www.techorama.nl/schedule/sessions/1241602).
It is a **.NET Aspire** solution that runs in **Docker**, showing:

- A deliberately **un-optimized** starting point for enterprise data
  (relational + document), not ready for safe agentic access.
- A progression through **10 practices** (`docs/10-practices.md`) that
  make the data agent-ready.
- **RAG** and **MCP** access patterns on the optimized data, orchestrated
  with the **Microsoft Agent Framework**.

Content is still being filled in — this file and the solution structure
will be expanded once the presentation deck is available. Keep that in
mind: prefer additive, easily-reversible changes over deep restructuring.

## Solution layout

See `docs/demo-briefing.md` for the authoritative, up-to-date project layout,
practice-to-code mapping, and build sequence. Summary (keep this table in
sync when projects are added/removed):

| Project | Purpose |
|---|---|
| `NICEDA.AppHost` | Aspire orchestrator — SQL Server + Cosmos DB emulator containers, wires connection strings/params, runs everything in Docker. |
| `NICEDA.ServiceDefaults` | Shared Aspire service defaults (telemetry, health checks, resilience). |
| `NICEDA.ApiService` | All data access (mocked CRM/SharePoint + real SQL/Cosmos), the before/after toggle, semantic/CQRS layer, RAG pipeline, and chat endpoint (Microsoft Agent Framework). Deliberately one process, not split across an extra agent project. |
| `NICEDA.Web` | Blazor UI only: data explorer (4 systems + Before/After toggle), agentic chat, current-user selector. No business logic here. |
| `NICEDA.McpServer` | The one deliberately-separate project: a real MCP server exposing governed write action(s), kept isolated from in-process tools. |
| `NICEDA.Tests` | xUnit tests, including the practice-9 deterministic test set. Grows alongside every slice — not a final step. |

Favor fewer projects/containers over "more realistic" architecture: this
is a 5-minutes-per-beat live demo, not a production system. Only split
something into its own project when the split itself is the point being
demonstrated (MCP vs. RAG separation is; an extra agent-host process
is not).

## Build & run

```powershell
dotnet build NICEDA.sln
cd NICEDA.AppHost
dotnet run
```

`dotnet test NICEDA.Tests` is the fast offline default. Live Docker tests
opt in using `NICEDA_TEST_API_URL`, `NICEDA_TEST_SQL`, and
`NICEDA_TEST_COSMOS` against an already-running AppHost (see README).
Never cold-start the distributed app inside each test run. Keep the
four-minute `test.runsettings` deadline enabled and pass cancellation
tokens through integration operations.

Aspire's dashboard will show links to the running services. Docker is the
target execution environment for the talk — containers are driven through
Aspire resource definitions in `NICEDA.AppHost`, not hand-written
Dockerfiles, unless a project specifically needs one.

## Working agreements

- Treat every change as a step in a before/after demo narrative. If you
  change how data is modeled or exposed, say which of the 10 practices it
  demonstrates (or update `docs/10-practices.md`).
- Keep RAG and MCP implementations separable so each can be demoed on its
  own and then combined — but don't manufacture extra separate projects
  beyond that one split; see "Solution layout" above.
- Never wire up an autonomous agent action that performs an unguarded
  destructive operation (raw `DROP`/`DELETE`/`UPDATE` without validation)
  — that class of mistake is explicitly one of the talking points of the
  session. Use the `niceda-demo-guard` Copilot CLI extension
  (`.github/extensions/`) as a tripwire while authoring.
- Don't invent production security/compliance scaffolding unless asked;
  this is a conference demo. Do leave a short comment where a real system
  would need more.
- **Warnings are errors.** `Directory.Build.props` sets
  `TreatWarningsAsErrors`. A build with warnings is a failed build — fix
  the warning, don't suppress it, before continuing.
- **Work iteratively, test as you go.** Each unit of work lands with its
  own test(s) in `NICEDA.Tests` in the same step — not a test pass saved
  for the end. After every slice: `dotnet build` (clean, no warnings) then
  `dotnet test` (green) before moving on or committing.
- **Bookmark practices in code.** Where a practice from
  `docs/10-practices.md` is demonstrated, add a short
  `// NICEDA-PRACTICE-N: ...` comment at the relevant spot so it's
  greppable/presentable live (`grep -rn "NICEDA-PRACTICE-" `).
- Keep commits small and scoped to one slice of `docs/demo-briefing.md`'s build
  sequence, so the repo history itself narrates before → after → agentic.

## Status

Architecture plan finalized in `docs/demo-briefing.md`. Implementation is
proceeding slice by slice per that plan's build sequence.
