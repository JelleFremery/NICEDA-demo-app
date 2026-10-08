# 10 Practices for Agent-Ready Enterprise Data

> Placeholder checklist for the Techorama 2026 session
> ["Optimizing data for agentic use"](https://www.techorama.nl/schedule/sessions/1241602).
> This will be filled in and refined once the accompanying slide deck is
> reviewed. Each practice should eventually link to the part of the NICEDA
> demo that illustrates it (before/after).

1. **Describe your data for machines, not just humans** — schemas, tables,
   and fields carry descriptions an agent/LLM can use for grounding.
2. **Curate, don't dump, the schema** — expose a minimal, purposeful surface
   instead of the full raw schema.
3. **Separate knowledge from operations** — decide per data source whether
   it's a RAG (semantic knowledge) concern or an MCP (operational/tool)
   concern.
4. **Define explicit permission boundaries** — agents act under scoped,
   least-privilege identities, never raw admin credentials.
5. **Govern and validate queries** — no free-form arbitrary queries;
   parameterized/allow-listed operations only.
6. **Make categorization and metadata first-class** — tag/classify data so
   agents (and retrieval layers) can reason about relevance and sensitivity.
7. **Design safe, idempotent tools** — MCP tools should be narrow,
   predictable, and safe to retry.
8. **Exclude unsafe autonomous actions** — some operations (destructive
   writes, irreversible actions) should never be agent-callable at all.
9. **Keep retrieval grounded and current** — RAG sources are indexed,
   chunked, and refreshed deliberately, not stale snapshots.
10. **Make everything observable** — log what agents retrieved, queried,
    and changed, so behavior is auditable after the fact.

## Status

This file is intentionally a stub. It will be expanded with the real
practices, examples, and before/after references once the presentation
content is shared.
