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

| Project | Purpose |
|---|---|
| `NICEDA.AppHost` | Aspire orchestrator — wires up all resources/services for local + Docker runs. |
| `NICEDA.ServiceDefaults` | Shared Aspire service defaults (telemetry, health checks, resilience). |
| `NICEDA.ApiService` | Backend API over the enterprise data. |
| `NICEDA.Web` | Front-end / demo UI. |

Additional projects for RAG, MCP, and the Microsoft Agent Framework
orchestration will be added as the demo narrative is built out — keep this
table updated when they land.

## Build & run

```powershell
dotnet build NICEDA.sln
cd NICEDA.AppHost
dotnet run
```

Aspire's dashboard will show links to the running services. Docker is the
target execution environment for the talk — containers are driven through
Aspire resource definitions in `NICEDA.AppHost`, not hand-written
Dockerfiles, unless a project specifically needs one.

## Working agreements

- Treat every change as a step in a before/after demo narrative. If you
  change how data is modeled or exposed, say which of the 10 practices it
  demonstrates (or update `docs/10-practices.md`).
- Keep RAG and MCP implementations separable so each can be demoed on its
  own and then combined.
- Never wire up an autonomous agent action that performs an unguarded
  destructive operation (raw `DROP`/`DELETE`/`UPDATE` without validation)
  — that class of mistake is explicitly one of the talking points of the
  session. Use the `niceda-demo-guard` Copilot CLI extension
  (`.github/extensions/`) as a tripwire while authoring.
- Don't invent production security/compliance scaffolding unless asked;
  this is a conference demo. Do leave a short comment where a real system
  would need more.
- Validate changes with `dotnet build` (and tests, once added) before
  considering a step done.

## Status

Scaffolding only. A PowerPoint with the full talk content is expected next
and will drive the concrete data model, the 10 practices examples, and the
RAG/MCP/tool implementations.
