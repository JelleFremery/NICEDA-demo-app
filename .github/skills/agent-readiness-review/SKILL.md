---
name: agent-readiness-review
description: Use when evaluating whether a data source, table, API, or document store in the NICEDA demo is "agent-ready" — i.e. when the user asks to review, audit, or score data against the 10 agent-readiness practices, or wants help identifying overexposed schemas, missing permission boundaries, or unsafe autonomous actions.
---

# Agent Readiness Review

This skill reviews a data source against the 10 practices documented in
`docs/10-practices.md` for preparing enterprise data for agentic use
(RAG + MCP), as presented in the Techorama 2026 session
"Optimizing data for agentic use".

## When to use

- The user asks you to "review", "audit", or "score" a table, API, or
  document collection for agent readiness.
- The user asks which of the 10 practices a given data source violates.
- The user is deciding between RAG and MCP for a specific data source.

## How to perform the review

1. Read `docs/10-practices.md` for the current list of practices (it is the
   source of truth; this skill does not duplicate it).
2. Read the target schema/API/collection definition the user points you at.
3. For each practice, mark it as met, partially met, or not met, with a
   one-line reason tied to concrete evidence in the code/schema — do not
   guess.
4. Call out explicitly:
   - Any overexposed schema (raw tables/columns with no curation).
   - Any missing permission/role boundary for agent access.
   - Any operation that should never be exposed to an autonomous agent
     (e.g. unguarded writes/deletes).
5. Recommend RAG, MCP, or both, with a one-sentence rationale.
6. Keep the output short: a compact table or checklist, not an essay.

## Notes

- This skill is read-only analysis. If the user wants enforcement, point
  them at the `niceda_check_agent_readiness` tool and the
  `niceda-demo-guard` hook (`.github/extensions/niceda-demo-guard/`), which
  flag destructive SQL before it runs.
