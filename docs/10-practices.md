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

All ten practices have code bookmarks below. Live model responses require
a configured Foundry credential; deterministic checks do not.

- **Practice 1 (data inventory)** — ✅ `docs/data-inventory.md` plus the
  live "Before" seed: `NICEDA.ApiService/Crm`, `Sql/RawSqlSeeder.cs`,
  `SupportDocs/RawSupportSeeder.cs`, `Legal`. Browse all four raw shapes
  (and the inventory itself) side by side at `/data-explorer` in
  `NICEDA.Web` — this is the ~slide 12 demo beat.
- **Practice 2 (identify agent use cases first)** — ✅
  `docs/agent-use-cases.md`, written before any retrieval/tool code; the
  3 use cases there drive the semantic layer and MCP tool design in later
  slices.
- **Practice 3 (semantic layer)** — `Sql/OptimizedSqlSeeder.cs`,
  `Sql/CustomerHealthQueries.cs`, `Crm/CustomerProfile.cs`,
  `Legal/LegalKnowledge.cs`, and `DataInventory/SemanticGlossary.cs`.
  The read view is over the original SQL rows, not a duplicated database.
- **Practice 4 (ingestion classification)** and **Practice 7 (meaningful
  chunks)** — `SupportDocs/OptimizedSupportSeeder.cs`. The three Contoso
  security topics become individually addressable chunks with
  classification, region, owner, retention, and source IDs.
- **Practice 6 (deletion)** — `DataLifecycle/CustomerDeletionCommand.cs`.
  SQL children are deleted transactionally, Cosmos documents by customer
  partition; durable SQL deletion markers prevent seed resurrection.
- **Practice 8 (CQRS)** — `Sql/CustomerHealthQueries.cs` and
  `Sql/FollowUpTaskCommands.cs`. Validate named commands, query named read
  models; never hand an agent a writable raw SQL interface.
- **Practice 9 (deterministic checks)** — `NICEDA.Tests/SeedDataTests.cs`,
  `SemanticProjectionTests.cs`, and `FollowUpValidationTests.cs` are fast
  offline checks. `OptimizedLayerTests.cs` and `CustomerDeletionTests.cs`
  explicitly opt into live SQL/Cosmos checks; the test runner has a
  four-minute total deadline.
- **Practice 5 (authorization before retrieval)** —
  `Security/DemoIdentity.cs` and `Rag/KnowledgeSearchService.cs`.
  Trusted identity filters candidates before cosine ranking, in both
  modes, and filters source text before embedding. The two immutable demo
  scopes have separate caches; admin's vectors are never reused for Jane.
  The Before demo exposes poor structure, not an intentional
  authorization bypass. Security metadata is excluded from embeddings.
  `Endpoints/DataExplorerEndpoints.cs` also applies these scopes to all
  four explorer systems in Before and After. The Web client sends the
  selected identity and reloads when it changes; missing or unknown API
  identities are rejected, never treated as admin.
- **RAG** — `Rag/FoundryEmbeddingService.cs` builds real embeddings from
  Foundry. Offline deterministic tests inject an explicitly fake embedding
  service; the running application never silently switches to it.
- **Practice 10 (feedback)** — `Feedback/FeedbackService.cs` and
  `Feedback/CosmosFeedbackStore.cs` retain structured outcomes, failures,
  and interactions in a real Cosmos `feedback` container. Customer-linked
  feedback and cached vectors participate in
  `DataLifecycle/CustomerDataLifecycle.cs` deletion. Startup retries
  durable deletion markers through that lifecycle; task and feedback
  writes share the same per-customer transaction lock.
- **Agent Framework and MCP** — `Agent/AgentChatService.cs` combines
  retrieval, the customer-health read model, and the separate
  `NICEDA.McpServer` follow-up tool. `GovernedTransportTests.cs` verifies
  the real HTTP MCP action, identity scope, and Before-mode write denial.
  `NICEDA.Web` shows the shared mode/identity, citations, traces, and
  feedback controls. Before omits the optimized health capability; After
  exposes it. Trusted idempotency keys bind action retries to a single
  SQL task, with conflicts rejected rather than repeated writes.
  `Agent/AgentChatOutcomeRecorder.cs` preserves completed outcomes and
  reports a logged warning if feedback storage fails.
