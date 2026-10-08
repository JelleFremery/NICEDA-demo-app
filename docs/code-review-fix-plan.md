# NICEDA code review and fix plan

Reviewed 8 October 2026. **All six fixes are now implemented.** The
original findings and implementation sequence below are retained as the
review record; the resolution table describes the completed behavior.

Scope: Before/After behavior, agent/tool contracts, RAG authorization,
feedback/action reliability, deletion, startup, Docker orchestration,
and deterministic tests. Existing demo identity and mocked CRM/SharePoint
are intentional; production authentication scaffolding is not requested.
Offline checks passed (59 passed, five live checks explicitly skipped)
and the solution built with zero warnings/errors during review.
Successful live Foundry generation was not exercised.

## Completed fixes

| Finding | Resolution | Regression coverage |
|---|---|---|
| R1 | Default Jane samples target Techorama; Contoso is explicitly labeled an admin example. | Browser checks of default identity, sample buttons, and mode/identity navigation. |
| R2 | Before omits `get_customer_health`; agent instructions and UI state that it is unavailable. After alone exposes the optimized health read. | Mode-specific tool contract assertions and browser availability text. |
| R3 | Completed actions/task IDs remain visible when outcome-feedback storage fails, with an explicit logged warning. Trusted idempotency keys propagate from Web through Agent/MCP/API to SQL; same-key retries reuse the task, conflicting payloads are rejected. Identity aliases resolve to canonical identities. | Failing feedback-store tests, key validation, real API and HTTP MCP retry/conflict/persistence checks. |
| R4 | Startup reconciles durable deletion markers through the full lifecycle before seeding support data. SQL task and Cosmos feedback writes share a per-customer SQL transaction lock with deletion and reject marked customers. | Scratch-customer failure after SQL commit, remaining Cosmos evidence, rejected new feedback, then fresh lifecycle/mock/vector reconciliation. |
| R5 | Startup is the application caller of full-lifecycle reconciliation. Tests exercise SQL, both support containers, feedback, mock projections, and vectors; no autonomous delete tool was added. | Offline repeated-reconciliation test and full live lifecycle test against real SQL/Cosmos. |
| R6 | Sources are filtered before any document embedding; indexes are separate for the two immutable demo scopes and modes. Ranking still filters, and deletion removes every cached scope and rechecks snapshots after query embedding. | Captured embedding inputs, admin-to-Jane cache isolation, aliases, empty authorized corpus, and deletion during both index construction and query embedding. |

The solution builds with zero warnings/errors. The final live suite
passes all 82 checks in approximately one second; offline runs pass 75
checks and explicitly skip seven live checks. Test sessions retain the
four-minute deadline. Live validation also caught and fixed a Cosmos
feedback ID projection serialization mismatch that prevented cleanup.

The remaining external verification is successful Foundry generation
with a rotated credential and deployed models; no runtime fake response
or embedding fallback has been introduced.

## Findings

P1 means fix before relying on this in the talk; P2 means close the
practice/coverage gap before presenting that capability as complete.

| ID | Priority | Location | Problem and consequence |
|---|---|---|---|
| R1 | P1 | `NICEDA.Web\Components\Pages\Chat.razor:56-60` | The default user is Jane, but two sample prompts target Contoso, outside her account scope. A presenter following the default UI gets denied/empty retrieval or a rejected action rather than the intended successful demo. |
| R2 | P1 | `NICEDA.ApiService\Agent\AgentChatService.cs:97-100,126-139` | Before and After both expose `get_customer_health`, which always queries `optimized.CustomerHealthView`. The same risk question is not a genuine raw-versus-optimized SQL comparison. |
| R3 | P1 | `NICEDA.ApiService\Agent\AgentChatService.cs:154-175,218-249` | Chat waits for feedback persistence after the agent/action completes. If feedback storage fails after a successful task creation, the client sees a failed request despite the committed action. Retrying can create another task. |
| R4 | P2 | `NICEDA.ApiService\DataLifecycle\CustomerDeletionCommand.cs:40-88`; `SeedDataHostedService.cs:52-63`; support seeders | SQL commits its deletion marker before Cosmos cleanup. After a Cosmos failure, restart prevents reseeding but does not finish deleting existing documents. The data explorer can still show the remnants. Retrying the deletion steps is safe, but there is no restart reconciliation. |
| R5 | P2 | `NICEDA.ApiService\Program.cs:53-54`; `DataLifecycle\CustomerDataLifecycle.cs:18-29`; `NICEDA.Tests\CustomerDeletionTests.cs` | The full lifecycle is registered but has no application caller, and deletion tests call the lower-level SQL/Cosmos command. The promised combined removal of mocks, vectors, and feedback is not covered end-to-end. This is a reachability/verification gap, not a reason to expose an unguarded delete tool. |
| R6 | P2, policy clarification | `NICEDA.ApiService\Rag\KnowledgeSearchService.cs:27-30,108-114`; `NICEDA.Tests\RagKnowledgeSearchTests.cs:31` | First-use indexing embeds all non-deleted documents before filtering candidates for the caller. Jane can trigger processing of out-of-scope content by Foundry. Result authorization still happens before ranking: this is **not evidence that Jane receives those documents**. A trusted whole-corpus ingestion job may legitimately do this; lazy caller-triggered indexing needs an explicit processing boundary or a stronger pre-embedding filter. |

## Implementation sequence

### 1. Align the default demo experience (R1)

Keep Jane as the default to make scoped access visible. Change the two
Contoso example prompts to an account Jane can access (for example,
Techorama). Add a clearly labeled admin/Contoso example or explanatory
text rather than silently changing identity.

Verify initial Jane state, both example prompts, identity persistence
through navigation, and the intended admin-only Contoso path. Do not
assert a particular model sentence; assert that the chosen customer is
authorized and that the action reaches the correct guarded command.

### 2. Make the Before tool contract honest (R2)

Make `get_customer_health` mode-aware. Prefer a small Before read path
returning the existing normalized facts/status codes, without the
optimized business projection. Keep After using the semantic view. If
that adds disproportionate code, omit that capability from Before and
make its unavailability explicit in the tool contract/UI.

Choose and document that behavior before implementation; do not silently
use After data. Add deterministic tests for tool availability and data
shape in each mode, including proof that Before does not return fields
from the optimized health projection. Until fixed, the speaker notes use
a knowledge-summary prompt for the retrieval comparison instead.

### 3. Preserve committed action outcomes on feedback failure (R3)

Separate completion of the operational action from recording its
feedback. If the task succeeds and feedback fails, return the task ID,
successful tool outcome, and an explicit feedback-persistence warning.
Log the persistence failure; do not silently swallow it or return an
all-success response.

Add a lightweight interaction/action idempotency key so retrying the
same operation cannot create a second task. Propagate it through the
agent/MCP/API command boundary, with uniqueness at the write store; do
not deduplicate merely by reason text, since two legitimate tasks may
have the same reason.

Test successful action + failing feedback store, visible partial
completion, a same-key retry returning the existing task, and a
different-key action remaining independent. Keep it small and
demo-oriented; no general-purpose workflow engine is needed.

### 4. Make deletion restart-recoverable (R4)

Reuse durable SQL deletion markers as the source for startup
reconciliation. For marked customers, repeat idempotent raw/optimized
support and feedback cleanup before declaring seeding ready. Reapply
mock/vector tombstones. Alternatively record incomplete lifecycle
progress if markers alone cannot express completion.

Propagate/log cleanup failures and keep the startup deadline bounded.
Prevent new customer-linked writes while deletion is pending. Do not
restore deleted data, drop containers, or erase volumes as recovery.

Inject a failure after SQL commit, restart/reconcile, and assert no
remaining SQL children, support chunks, feedback, mocked projections,
or searchable vectors. Exercise retry and a second restart.

### 5. Exercise the full lifecycle safely (R5)

Add a scratch-customer integration scenario that calls
`CustomerDataLifecycle`, not just `CustomerDeletionCommand`, and verifies
all original and derivative stores. Use a uniquely named test customer;
never delete Betabit, Fenetre, Techorama, Contoso, or Fabrikam in normal
validation.

If a runnable maintenance entry point is needed, make it an explicit
developer-only operation with a validated customer ID and confirmation.
Do not add deletion to the autonomous MCP tool set. Keeping the talk's
deletion demo code-only is intentional and remains valid.

### 6. Make the embedding processing boundary explicit (R6)

For this small demo, prefer filtering source documents by resolved
identity before invoking the embedding provider. Cache by a clearly
defined authorized scope, and ensure deletion invalidates every scope's
cached vectors. Preserve filtering before ranking as a second boundary.

If keeping a global index, move whole-corpus embedding to a separately
identified trusted ingestion step and document that Foundry is authorized
to process the corpus independently of a requesting user. Do not imply
that a query-time result filter also limits data sent to the embedding
provider.

Update fake-embedding assertions to capture every submitted text. Under
Jane, no Contoso/Fabrikam text should be submitted in the caller-scoped
design. Check admin behavior, no authorized documents, warm-cache
isolation, and deletion during index construction. Security metadata
must remain outside embedding text.

## Validation gates for each change

1. Add focused deterministic regressions with the change; keep warnings
   as errors and run the smallest relevant selectors first.
2. Build the solution before launching AppHost on Windows. Run live tests
   against the warm AppHost with `--no-build`; do not cold-start Aspire in
   a test fixture.
3. Retain the four-minute test-run deadline. Escalate to all offline and
   opt-in SQL/Cosmos/MCP checks after the relevant focused tests pass.
4. Rehearse the three speaker-note sequences, including a feedback-store
   failure and a scope-denied question. Verify real model deployments
   with a rotated credential separately from deterministic tests.
5. Update `docs/10-practices.md`, the demo notes, and bookmark anchors if
   a surface moves or a comparison changes. Commit each coherent fix
   separately.

## Deliberate limitations, not bugs to "harden away"

- The dropdown is demo identity, not real sign-in; the presenter explorer
  exposes the inventory. Do not deploy publicly as-is.
- CRM/SharePoint are mocked; SQL/Cosmos are actual Docker-backed stores.
- Vector indexes and interaction lookup are in memory. Existing written
  feedback is durable, but feedback for a pre-restart interaction is
  rejected explicitly.
- Capturing feedback does not implement automatic learning/retraining.
- LLM answer quality is nondeterministic. Compare evidence and tool
  contracts, not a rehearsed promise that Before must always fail.
