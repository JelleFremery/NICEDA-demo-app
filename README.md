# NICEDA

Demo repository for the Techorama 2026 session
[**Optimizing data for agentic use**](https://www.techorama.nl/schedule/sessions/1241602)
by [Jelle Fremery](https://www.techorama.nl/schedule/sessions/1241602).

> Agentic AI is only as effective as the data it can securely access,
> understand, and act upon. This repo is the live, end-to-end demo for the
> talk: it starts from enterprise data that isn't ready for agentic use,
> walks through 10 practices to make it agent-ready, and then connects an
> agent orchestration (Microsoft Agent Framework) to that data using two
> complementary patterns — **RAG** (semantic knowledge retrieval) and
> **MCP** (governed, tool-based operational access).

## What you'll see here

1. **Before**: enterprise data (Azure SQL + Cosmos DB) modeled the way most
   line-of-business apps model it today — not designed for an autonomous
   agent to discover, query, or reason over safely.
2. **10 practices**: a checklist (`docs/10-practices.md`) for turning that
   data into secure, agent-ready capabilities — covering schema curation,
   permission boundaries, governed queries, and more.
3. **After**: the same data, reshaped, exposed through:
   - **RAG** for semantic grounding over document/knowledge data.
   - **MCP** for structured, governed tool access to operational data.
4. **Agent orchestration** built with the **Microsoft Agent Framework**,
   tying RAG + MCP + tools together into one agent.

> This repo is being built out incrementally alongside the talk's slide
> deck. Expect the data model, practices, and agent code to grow — this
> initial commit is scaffolding only.

## Tech stack

- **.NET Aspire** — orchestration of the solution's services/resources.
- **Docker** — the target run environment (Aspire drives container
  resources).
- **Azure SQL** — relational/operational enterprise data.
- **Cosmos DB** — document/knowledge data.
- **Microsoft Agent Framework** — agent orchestration, RAG, MCP, and tools.

## Solution structure

| Project | Purpose |
|---|---|
| `NICEDA.AppHost` | Aspire app host — orchestrates all resources/services. |
| `NICEDA.ServiceDefaults` | Shared Aspire service defaults (telemetry, health, resilience). |
| `NICEDA.ApiService` | Backend API over the enterprise data. |
| `NICEDA.Web` | Front-end / demo UI. |

See [`AGENTS.md`](./AGENTS.md) for agent working agreements and
[`docs/10-practices.md`](./docs/10-practices.md) for the practices
checklist.

## Getting started

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download) and
Docker Desktop (or another OCI-compatible container runtime) running.

```powershell
dotnet build NICEDA.sln
cd NICEDA.AppHost
dotnet run
```

Open the Aspire dashboard link printed in the console to see the running
services and their logs/telemetry.

## Repo tooling for Copilot CLI

This repo ships a couple of small GitHub Copilot CLI aids used while
building the demo (not part of the demo app itself):

- `.github/copilot-instructions.md` — repo-wide Copilot instructions.
- `.github/skills/agent-readiness-review/` — a skill that reviews a data
  source against the 10 practices.
- `.github/extensions/niceda-demo-guard/` — a tool
  (`niceda_check_agent_readiness`) plus a safety hook that flags
  destructive SQL before an agent runs it.

## Status

🚧 Scaffolding stage. Next step: incorporate the presentation content to
flesh out the data model, the 10 practices, and the RAG/MCP/agent demo.
