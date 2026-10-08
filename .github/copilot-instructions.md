# Copilot instructions for NICEDA

NICEDA ("eNterprIse data for CopilotEDA" — demo repo) is the companion
solution for the Techorama 2026 talk
[**Optimizing data for agentic use**](https://www.techorama.nl/schedule/sessions/1241602)
by Jelle Fremery. It is a .NET Aspire solution, run in Docker, that
demonstrates:

1. A **before** state: enterprise data (Azure SQL + Cosmos DB) that is not
   ready for agentic consumption (overexposed schema, no descriptions, no
   permission boundaries, unsafe operations possible).
2. An **after** state: the same data reshaped using the 10 practices in
   `docs/10-practices.md`.
3. Two agentic access patterns on top of the "after" state, built with the
   **Microsoft Agent Framework**: simple **RAG** for semantic
   knowledge/grounding, and **MCP** for governed, tool-based operational
   access.

## Ground rules for changes in this repo

- This is demo/talk code: favor clarity and a clean before/after narrative
  over production hardening. Prefer the simplest implementation that makes
  the point of the current practice/step — fewer projects/containers beats
  "more realistic" every time a choice comes up.
- Keep the solution runnable via **.NET Aspire** (`dotnet run` on the
  AppHost) and via **Docker**. Don't introduce dependencies that can't run
  in a container.
- **Warnings are errors** (`Directory.Build.props`). A build with warnings
  is a failed build.
- **Iterate with tests.** Every slice of work adds its test(s) in
  `NICEDA.Tests` alongside the code, then `dotnet build` + `dotnet test`
  before moving on. Don't defer testing to a final pass.
- When adding or changing a "practice" demo step, update
  `docs/10-practices.md` and bookmark the spot in code with a
  `// NICEDA-PRACTICE-N: ...` comment so it's greppable during the talk.
- Prefer small, incremental commits per practice/step so the repo history
  itself can be shown during the talk. See `docs/demo-plan.md` for the
  exact build sequence.
- Don't add production-grade auth/security scaffolding unless it's the
  point being demonstrated — but do call out in comments/docs where a real
  deployment would need more (this talk is partly about security
  boundaries, so the gap should be visible, not silently papered over).
- Data sources: Azure SQL for relational/operational data, Cosmos DB for
  document/knowledge data. Keep the split intentional and explain in
  comments why a given demo artifact lives in one vs. the other.
- Agent/tool code uses the Microsoft Agent Framework. Keep RAG and MCP
  implementations clearly separated — `NICEDA.McpServer` is the one
  deliberate extra project for this; don't add more just for symmetry.

## Repo-specific tooling

- `.github/extensions/niceda-demo-guard/` — a Copilot CLI extension used
  while building this repo: a tool to sanity-check a data source
  description against the 10 practices, and a hook that pauses on
  obviously destructive SQL. It's a repo-authoring aid, not part of the
  demo app itself.
- `.github/skills/agent-readiness-review/` — a skill for reviewing a data
  source against `docs/10-practices.md`.

See `docs/demo-plan.md` for the current architecture and build sequence,
and `AGENTS.md` for the authoritative, tool-agnostic agent contract.
