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

The fictional customer seed includes **Betabit, Fenetre, Techorama,
Contoso, and Fabrikam**. All contacts, contracts, and incidents are invented
demo data, not claims about the named organizations.

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
| `NICEDA.McpServer` | Separate HTTP MCP service exposing the governed follow-up action. |
| `NICEDA.Tests` | Fast deterministic checks plus opt-in live Docker checks. |

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

### Tests

```powershell
dotnet test NICEDA.Tests
```

The normal suite requires neither Docker nor model credentials. Live
SQL/Cosmos checks use the same running AppHost instead of booting another
emulator for each test run. They are explicitly skipped unless enabled:

```powershell
$env:NICEDA_TEST_API_URL = "<apiservice HTTP endpoint from the dashboard>"
$env:NICEDA_TEST_SQL = "<niceda connection string from the dashboard>"
$env:NICEDA_TEST_COSMOS = "<ConnectionStrings__cosmos from apiservice's dashboard environment>"
$env:NICEDA_TEST_MCP_URL = "<mcpserver HTTP endpoint from the dashboard>/mcp"
dotnet test NICEDA.Tests --no-build --settings NICEDA.Tests\test.runsettings
```

Keep connection strings out of source control. The test runner has a
four-minute overall deadline; live HTTP requests have a 20-second timeout.
Build before starting the AppHost; running services lock their executable
files on Windows, so live checks use `--no-build`.
Start Docker before the talk: emulator cold-start is setup, not part of
the five-minute demo.

The data containers use persistent lifetime so restarting the AppHost
reuses the warm containers. If the Linux Cosmos emulator never becomes
ready, check its logs rather than waiting indefinitely: the vNext image
can retain a stale PostgreSQL `postmaster.pid` after an abrupt stop.
Repair that lock only after confirming no PostgreSQL process is running
in that specific emulator container; never delete the data volume as a
startup workaround.

### Foundry configuration

Data exploration and deterministic tests work without an LLM. Real chat
and embeddings require a configured key; missing credentials produce an
explicit unavailable response, never a simulated answer.

```powershell
dotnet user-secrets set "Foundry:ApiKey" "<rotated key>" --project NICEDA.AppHost
dotnet user-secrets set "Foundry:Endpoint" "https://bblc-we-ai-foundry.services.ai.azure.com/" --project NICEDA.AppHost
```

Aspire passes these settings to the API. Defaults are chat model
`gpt-6.1-sol` and embedding model `text-embedding-3-small`; override using
`Foundry:ChatModel` and `Foundry:EmbeddingModel`. The endpoint is normalized
to its OpenAI-compatible `/openai/v1` base. Agent Framework uses the
Responses API: the default reasoning deployment does not support
function tools through Chat Completions.
The installed SDK still labels Responses experimental; the demo explicitly
opts in only at the adapter factory and its regression test (`OPENAI001`),
not solution-wide.
Rotate the key pasted into chat
before using it for the talk.

### Docker deployment

The AppHost includes a Docker Compose publishing environment. With the
Aspire CLI installed, publish using `aspire publish --environment compose`.
Publishing generates the Compose definition with image/secret placeholders,
not running containers. Use `aspire deploy --environment compose` to build
images and run it. No manually-maintained Dockerfiles are required. Local
`dotnet run` launches .NET services on the host and the two data stores in
Docker. Startup waits at most two minutes for the stores and creates the
demo SQL database when using Compose.

### Five-minute demo beats

Paste-ready slide notes are in
[`docs/demo-speaker-notes.md`](./docs/demo-speaker-notes.md).
The native Visual Studio bookmark setup and code tour are in
[`docs/demo-bookmarks.md`](./docs/demo-bookmarks.md).
Known issues and the prioritized repair sequence are in
[`docs/code-review-fix-plan.md`](./docs/code-review-fix-plan.md).

1. **Before:** open Data Explorer, select Before, tour CRM's many fields,
   SQL joins, inconsistent support JSON, and Legal folder paths.
2. **After:** keep the customer/system selected and flip to After; show
   business projections, classifications, concepts, and source IDs.
3. **Agentic:** open Chat, ask the same question in each mode, then request
   a follow-up. Show retrieved citations, tool traces, and Helpful/Not
   helpful feedback. Jane sees her EU accounts; admin sees all.

Chat interactions are held in memory; submit feedback before restarting
the API. Feedback already submitted is durable in Cosmos, but a feedback
submission for an interaction from before a restart is explicitly rejected
as unknown.

The user dropdown is **demo identity, not authentication**. Changing it
reloads the selected explorer system in both Before and After: Jane sees
only her EU customers; admin sees all records, including internal Legal
documents. Record endpoints require the `X-Niceda-User` header (`jane` or
`admin`); missing or unknown identities return 403. The inventory and
glossary remain presenter-wide. Real
deployment needs authenticated identities and protected internal API/MCP
endpoints; do not expose this app publicly as-is.

Before chat exposes knowledge retrieval, not the optimized
`get_customer_health` capability. After adds customer health and the
follow-up action. Jane's examples target Techorama; select admin for the
Contoso walkthrough. Document text is authorized before being sent for
embedding, with separate indexes for Jane and admin.

Follow-up requests carry a trusted idempotency key through the MCP/API
boundary. A retry with the same identity, key, and payload returns the
existing task; a changed payload is rejected. The chat UI keeps a pending
key across failures/warnings and starts a new operation after an
unqualified success. Direct `/api/chat` callers must supply a 1-100
character `requestIdempotencyKey`, and direct MCP/action callers send
`X-Niceda-Idempotency-Key`. Completed task IDs/outcomes remain visible
with a warning if outcome-feedback persistence fails.

Startup finishes deletions recorded by durable SQL markers before
seeding support data. Customer-linked task/feedback writes serialize with
deletion and reject marked customers. The live deletion regression uses
a scratch customer and checks original and derivative stores; do not
delete seeded demo customers to reset the talk.

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

See [`docs/demo-plan.md`](./docs/demo-plan.md) for the three five-minute
demo beats and [`docs/10-practices.md`](./docs/10-practices.md) for code
bookmarks. SQL and Cosmos are real Docker-backed stores; CRM and Legal
are deliberately mocked.
