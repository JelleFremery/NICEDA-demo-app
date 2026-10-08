# 10 Practices for Agent-Ready Enterprise Data

Authoritative checklist for the Techorama NL 2026 session
["Optimizing data for agentic use"](https://www.techorama.nl/schedule/sessions/1241602).
This is the real, final numbering (reconciled with the deck's own
practice slides — see `docs/demo-plan.md` §5 for the full mapping and
deck source references). Each practice links to where it's demonstrated
in code via a `// NICEDA-PRACTICE-N: ...` comment — grep for that tag to
find every live example.

1. **Build a data inventory** — know what data you have, per system:
   what it is, who owns it, how it's classified, and its agent potential.
   Demonstrated via the NICEDA data inventory (CRM/SQL/Cosmos/SharePoint)
   in `docs/demo-plan.md` §2 and the seed/catalog code.
2. **Identify agent use cases first** — decide what decisions the agent
   must support *before* touching retrieval/embeddings. Demonstrated via
   `docs/agent-use-cases.md` driving what the semantic layer and tools
   expose.
3. **Add a semantic layer** — business-meaning views/projections
   (`CustomerHealthView`-style) and canonical-term/alias glossaries,
   replacing raw joins and column codes the agent would otherwise have to
   reverse-engineer.
4. **Classify during ingestion** — classification, owner, retention, and
   region are stamped at write time, never bolted on after the fact.
5. **Separate authorization and retrieval** — identity/security context is
   resolved before the semantic/vector filter runs; security metadata
   travels with the data but is never embedded.
6. **Design for deletion** — a single customer-deletion path fans out
   across every store (SQL rows, Cosmos docs, vector index) so "delete"
   actually means deleted everywhere.
7. **Chunk based on meaning** — documents are split by business concept
   (onboarding, security review, escalation), not by fixed character
   count.
8. **Apply the CQRS pattern** — expose business capabilities
   (`GetCustomerHealth`, `CreateFollowUpTaskCommand`), never raw
   `SELECT *`/table access, to anything agent-facing.
9. **Verify the use case with automated deterministic tests** — fixed
   question → expected-facts test sets prove retrieval/action quality;
   "the AI seems good" is not a test.
10. **Close the loop (create feedback data)** — agent tool outcomes,
    failures, and user interactions are written back as structured data,
    linked to the originating use case/semantic entity, so the system
    improves over time instead of just acting and forgetting.

## Status

Finalized for implementation. As each practice lands in code, this file
gains a short "where to look" pointer (file/tag) alongside its
description.

- **Practice 1 (data inventory)** — ✅ `docs/data-inventory.md` plus the
  live "Before" seed: `NICEDA.ApiService/Crm`, `Sql/RawSqlSeeder.cs`,
  `SupportDocs/RawSupportSeeder.cs`, `Legal`. Browse all four raw shapes
  (and the inventory itself) side by side at `/data-explorer` in
  `NICEDA.Web` — this is the ~slide 12 demo beat.
- **Practice 2 (identify agent use cases first)** — ✅
  `docs/agent-use-cases.md`, written before any retrieval/tool code; the
  3 use cases there drive the semantic layer and MCP tool design in later
  slices.
- Practices 3–10: not yet implemented (slices 3–6, see
  `docs/demo-plan.md` §9).
