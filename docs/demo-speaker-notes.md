# NICEDA: PowerPoint demo notes

These are paste-ready notes for the three demo slides in the deck:
slide 10 ("NICEDA: Before"), slide 40 ("NICEDA: After"), and slide 48
("NICEDA: Agentic use"). Each demo lasts five minutes in total; that is
not five minutes per practice.

If the deck has been reordered, use the slide titles. The deck's practice
numbers match `docs/10-practices.md` 1–10 (see `docs/demo-plan.md`
section 5 for the slide mapping). Bookmarks (B01–B21) are listed in
`docs/demo-bookmarks.md`.

## The cast: which customer proves what

Do not tour every customer. Each one has a single job in the story:

| Customer | Role in the story | Where it pays off |
|---|---|---|
| **Betabit** (Jane's, EU) | **Is this account at risk?** CRM status says "Active, watch closely", while the sales notes say the champion left and there is a competing quote. SQL says `AtRisk`. Support has the same customer in two document shapes. | Before: scattered meaning. After: one profile, view `RiskLevel = High`, one chunk envelope. |
| **Contoso** (NA, admin only) | **The stage question.** "Summarize Contoso's security review with sources." The answer sits in one raw assessment blob (`support-005`), which the After state turns into three concept chunks (`optimized-sup-005a/b/c`). | Every beat. Also the permission boundary for Jane. |
| **Fabrikam** (NA) | **Permissions come from policy, not text.** CRM lists Jane Okafor as account manager, yet Jane never sees Fabrikam: her scope is an explicit EU customer list (B13). | Before, last beat. Backup risk question. |
| **Techorama** (Jane's, EU) | **"Never touched"** in Agent usage. Only reliable on a fresh seed: broad search results can touch it. | Agentic, last beat (with caveat). |
| **Fenetre** (Jane's, EU) | **Healthy control.** Renewed, ~60k, view `RiskLevel = Low`. Shown next to Betabit, it proves the risk rule and the agent tell accounts apart instead of flagging everyone. | After SQL beat; Jane's risk question (backup). |

## Proof map: concept → evidence → bookmark

| Practice | What the audience sees | Prompt / action | Code proof |
|---|---|---|---|
| 1 Inventory | Four systems, owners, classification | Before > any tab | B01 |
| 2 Use cases | Three questions plus one action drive the design | (slide) | B02 |
| 3 Curated concepts | Betabit: dozens of CRM fields → nine-field profile with `SourceSystem`/`SourceId` | After > CRM flip | B15 |
| 4 Classify at ingestion | Betabit legal file and Contoso chunks carry classification, owner, retention | After > SharePoint / Cosmos flip | B05, B16 |
| 5 Security before ranking | Jane: no Contoso evidence, nothing about Contoso embedded | Jane + hero prompt | B06, B17, B13 |
| 6 Lifecycle | Deletion removes vectors, feedback, audit rows | (say, do not run) | B07 |
| 7 Chunk by meaning | Before: 60-char fragments, top 2. After: 005a/b/c citations | Hero prompt Before vs After | B14, B05 |
| 8 CQRS / capabilities | Health view answers risk; task is a validated command; health tool is After-only | Hero prompt + task prompt | B04, B19, B10, B11 |
| 9 Deterministic tests | The stage question is a unit test on the real seed | (show test) | B18 (B08 backup) |
| 10 Feedback + audit | Feedback in Cosmos; every tool call in `audit.AgentActionLog`; Agent usage roll-up | Feedback buttons, Audit log, Agent usage | B12, B20, B21 |

## Before the audience arrives (not part of the demo clock)

1. Rotate the previously shared Foundry key, then configure AppHost user
   secrets as described in `README.md`. Confirm that both the chat and
   embedding deployments are available.
2. Start Docker. Build `NICEDA.sln` before launching the AppHost, then run
   `dotnet run --project NICEDA.AppHost --no-build`. Wait for seeding to
   complete. Do not cold-start the Cosmos emulator on stage.
3. Open the Web link from the Aspire dashboard. Check all six explorer
   tabs (CRM, SQL, Cosmos DB, SharePoint, Audit log, Agent usage) in
   Before and After. As **admin**, confirm all five customers appear.
   As **Jane**, confirm only Betabit, Fenetre and Techorama appear.
4. **Warm-up, and its side effect.** Open Chat as **admin** and submit the
   hero prompt (below) once in Before and once in After. This verifies
   model access and warms both embedding indexes. Inspect the real
   answers, citations and traces; do not assume Before will always be
   worse. Every search hit writes an audit row, so warming can mark
   Techorama as "touched" in Agent usage. Look at Agent usage now and
   decide which zero cell you will point to (see Agentic, last beat).
5. Write the follow-up due date into your notes:
   `(Get-Date).ToUniversalTime().AddDays(14).ToString('yyyy-MM-dd')`.
   Task creation changes data: do not repeat it just to warm the model,
   and do not delete a seeded customer to reset the demo.
6. Run the normal tests (`dotnet test NICEDA.Tests`). For live checks,
   follow the opt-in variables in `README.md` and use `--no-build` while
   the AppHost is running.
7. In the open NICEDA Visual Studio solution, run
   `.\tools\Set-DemoBookmarks.ps1` from Developer PowerShell. Open
   **View > Bookmark Window** (`Ctrl+K, Ctrl+W`). Pre-open the files for
   B01, B04, B05, B06, B14, B17, B18, B19, B10 and B20 as tabs.
8. Set a readable font size. Keep the browser, Visual Studio and the
   presentation adjacent. Start on **Data explorer > Before > CRM**, with
   **admin** selected.

Do not show the dashboard's connection-string environment or user secrets
on the projector. The identity dropdown is a demo security context, not
authentication.

### The prompts (copy these into your notes)

- **Hero prompt** (slide 47's workflow in one sentence):
  `Summarize Contoso's security review with sources, check Contoso's current account health, and recommend one follow-up action.`
- **Task prompt** (complete on its own; each chat submission is a new run
  with no memory of the previous answer):
  `Create one follow-up task for Contoso. Reason: Techorama live demo - add a dedicated Contoso escalation contact. Due date: [YYYY-MM-DD].`
- **Backup, Jane:** `Which of my customers are at risk and why?`
  (After: Betabit High with reasons; Fenetre Low as the healthy control —
  "it didn't cry wolf"; no Fabrikam.)
- **Backup, Jane:** the task prompt above. The result is an explicit MCP
  authorization failure and a failed audit row.
- **Backup, admin:** `What are Contoso's open risks?` (slide 42's example).

## Slide 10: "NICEDA: Before"

**Opening line:** "Before we build an agent, let's look at what it would
have to understand. Keep two questions in mind: *is Betabit at risk?* and
*summarize Contoso's security review with sources.*"

| Time | Action | Speaker notes |
| --- | --- | --- |
| 0:00-0:50 | Browser: admin > Before > CRM. Find the **Betabit** row; scroll to status, contract value and sales notes. | "Is Betabit at risk? Status says 'Active, watch closely'. The real answer sits in a sales note: the champion left, there's a new CTO and a competing quote. The value is '~€180k/yr', next to 'about 60k EUR annually' and '$310,000/yr'. 'Renews in about six weeks' — six weeks from when?" |
| 0:50-1:30 | Select SQL. Point at Betabit's `StatusCode = AtRisk`, `AnnualValue`, `EndDate`. | "This is a real SQL Server. Here Betabit *is* `AtRisk`, but only after a join and a code lookup. And the end date is a year out, while CRM says six weeks. Which source is right? An agent can't decide that; an owner has to." |
| 1:30-2:40 | Select Cosmos DB. Show Betabit `support-001` vs `support-002` (two shapes), then open Contoso's `support-005`. | "This is a real Cosmos database. Same customer, two ticket shapes: `ticketId` here, `ticket_number` with nested details there. And here is the answer to our Contoso question: one blob covering access controls, residency and incident response. No concept, no classification, no region. An agent gets all of it, or a random slice." |
| 2:40-3:10 | Select SharePoint. Point at `/Customers/Betabit/Compliance/Security_Questionnaire_Response.pdf`. | "Legal knows where to file things. But 'Compliance' in a folder path isn't a classification, an owner, or a retention rule. CRM and SharePoint are mocked; SQL and Cosmos are real." |
| 3:10-3:50 | Visual Studio: **B01** (inventory). Mention B02. | "So, first step: inventory the systems, owners, classifications and agent potential. Then choose the three questions and one action we actually need (B02). We don't hand the agent a database and hope." |
| 3:50-5:00 | Browser > CRM. Point at **Fabrikam**'s account manager: Jane Okafor. Switch to **Jane**: Fabrikam and Contoso disappear. Switch back to admin. | "CRM says Jane manages Fabrikam, yet she can't see it. Her scope is an explicit EU customer list, not a free-text field. So Before isn't insecure: permissions are enforced. The problem is understanding. Next, we reshape the same data." |

**Return to slides:** "Information architecture is part of agent
architecture."

**If short on time:** skip SharePoint and B01. Keep Betabit CRM → SQL,
the Contoso blob, and the Jane/Fabrikam switch. Do not start chat in this
demo.

## Slide 40: "NICEDA: After"

**Opening line:** "We haven't replaced a single system. We've made their
data usable for specific agent decisions. Same two questions."

Navigation rule for this demo: **stay on one tab and flip Before ↔ After**.
Don't tour all four systems again. The contrast is the point.

| Time | Action | Speaker notes |
| --- | --- | --- |
| 0:00-0:40 | Browser: admin > CRM, **Betabit**. Flip Before → After. | "Same customer: nine curated fields, a health summary built from the notes, a classification, and `SourceSystem`/`SourceId` back to the original. This is a projection over the original entry, not a second CRM." (B15 if asked) |
| 0:40-1:20 | SQL; flip → After. Point at `RiskLevel`: Betabit **High**, Fenetre **Low** (healthy control), Contoso **Low**. | "Is Betabit at risk? One row, in business words. And Fenetre, which renewed, stays Low, so the rule tells accounts apart. The view owns the rule: `AtRisk` means High, renewal within 90 days means Medium. The glossary tells the agent what `RiskLevel` means and to still check CRM and support evidence. The CRM-versus-SQL date conflict becomes an owner's decision, not an agent's guess." |
| 1:20-2:20 | Cosmos DB; flip → After. Show Contoso `optimized-sup-005a/b/c`: concept, `sourceId = support-005`, classification, region, retention. Then show Betabit's single envelope. | "Remember the blob? It's now three chunks by meaning: access controls, residency, incident response. Each carries policy metadata stamped at ingestion, so an answer can cite exactly one. And Betabit's two ticket shapes now share one envelope." |
| 2:20-2:40 | SharePoint; flip → After on the Betabit questionnaire. | "Now it has a concept, `Restricted`, an owner — Legal — and seven-year retention. The original path is still there for traceability." |
| 2:40-3:20 | Visual Studio: **B04** semantic view, **B05** chunk envelope. | "Small, readable transformations. The view gives business vocabulary; the envelope gives retrieval meaning and policy." |
| 3:20-4:00 | **B06** then **B17**. One sentence on B07. | "Identity filters the sources *before* anything is embedded or ranked. Policy fields filter the chunk but are never embedded. And deletion" (B07, don't open) "removes derivative data too: vectors, feedback, audit rows." |
| 4:00-5:00 | **B18**: the stage-question test. | "This is the proof. The exact question I'll ask the agent is a unit test on the real seed. After must rank the three Contoso concepts first; Before only gets concept-less fragments; Jane never even embeds Contoso text. No LLM grades another LLM. It's offline and runs in milliseconds." |

**Return to slides:** "Optimization isn't just adding embeddings. It's
adding meaning, policy, lifecycle and testable capabilities."

**If short on time:** do SQL and Cosmos only, then B06 and B18. Keep B07
and B08 as open backup tabs.

**Deletion detail if asked:** at startup, durable deletion markers are
retried through the full lifecycle before seeding. The real-store
regression test uses a scratch customer, fails after SQL commits, then
verifies restart reconciliation across original and derivative data. Do
not delete a seeded customer live.

## Slide 48: "NICEDA: Agentic use"

This follows slide 47 ("Getting it to work"). The hero prompt is that
slide's workflow: retrieve conversations, query live account status,
analyse risk, recommend an action. Authorization and approved tasks
follow.

**Opening line:** "Now let's give an agent this data. Knowledge retrieval
and operational actions are different capabilities, and we'll see both."

| Time | Action | Speaker notes |
| --- | --- | --- |
| 0:00-0:50 | Chat: admin, **Before**. Submit the **hero prompt**. Open the trace. | "Look at what the agent got: two 60-character fragments with no concept. And there's no health tool at all, because Before has no governed view to expose. Judge the evidence, not how convincing the prose sounds." (B14 if asked) |
| 0:50-1:50 | Flip to **After**; submit the identical prompt. | "Same model, same identity, same question. Now the citations are `optimized-sup-005a/b/c`, `get_customer_health` returns Contoso as Low risk, and the recommendation follows from the incident-response finding: a dedicated escalation contact. This is a live comparison, not a promise that every answer will be better." |
| 1:50-2:40 | Still admin, After. Submit the **task prompt** with your date. | "Each chat turn is a fresh run, so the action request is complete on its own. This is the real, separate HTTP MCP service calling a validated command. Here's the task ID. Success means a persisted task, not that someone got an email. In Before mode, the MCP tool refuses writes." |
| 2:40-3:00 | Add a short comment and click Helpful or Not helpful. | "Feedback becomes structured data in Cosmos, linked to the interaction and the customer. It does not retrain the model." |
| 3:00-3:40 | Select **Jane**, After; submit the **hero prompt**. Show the trace. Switch back to admin. | "Jane's scope doesn't include Contoso. Search finds no Contoso evidence, because it was never embedded for her, and the health tool returns *denied*. Success here means no data leaks. Any unrelated EU citations are not Contoso evidence." |
| 3:40-4:15 | Visual Studio: **B19** After-only tool, **B10** MCP tool, **B20** audit hook. | "Capabilities are explicit: the health tool exists only in After. The write is a separate MCP service; identity comes from request context, never from a model-controlled argument. And every tool call — including that denial — goes through one audit hook." |
| 4:15-5:00 | Browser: Data explorer > **Audit log** (admin, newest first). Point at Jane's **denied** `get_customer_health` row and admin's `create_follow_up_task` row. Then **Agent usage**. | "Everything we just did is a row: mode, who the agent acted for, the customer, the outcome. Roll that up and slide 43's question answers itself: which data does the agent never touch? Here, nobody has asked the agent to create a task for Fenetre or Techorama. That's data to improve, or to stop maintaining." |

**Evidence checkpoints**
- Before (admin) retrieval returns two concept-less fragments; they may or
  may not come from `support-005`.
- After can cite `optimized-sup-005a/b/c`, all traceable to `support-005`.
  The exact wording and subset of citations depend on the model, so don't
  promise all three every time.
- Jane must never receive Contoso sources; her `get_customer_health` call
  for Contoso is logged as denied.
- Agent usage zero cells:
  - `create_follow_up_task` for Fenetre/Techorama is always reliable.
  - "Techorama never touched" holds only on a fresh seed with no broad
    search (see prep step 4).

**Important comparison boundary:** the hero prompt compares *both* data
shape and capability. Before has no health tool by design. To isolate
retrieval quality only, use "Summarize Contoso's security review with
sources." on its own. An empty or denied answer for Jane does not mean
After retrieval is broken; it is the permission boundary working.

**If short on time:**
- Skip feedback and B19.
- Prioritize one evidence-bearing After answer, one persisted MCP action,
  and the Jane denial row in the Audit log.

## Honest fallback (all three demos)

If a service fails, show the explicit error and name the missing
dependency. Do not restart Docker or troubleshoot credentials on stage.
Use the prepared code tabs and the latest successful test results instead
(B18 shows the stage question passing offline). If you capture
screenshots during rehearsal, label them as rehearsal captures, not live
output.

Missing Foundry credentials intentionally return an unavailable response,
not a simulated answer. Feedback must be submitted before the API
restarts, because interaction lookup is in memory. Feedback that was
already written stays durable in Cosmos.

Completed actions appear with their task IDs even if outcome-feedback
persistence fails; a warning accompanies them. Retrying the same pending
request in the same chat view reuses its idempotency key, so SQL returns
the existing task or rejects a changed payload. A new request gets a new
key and represents a new action. After an uncertain network outcome, do
not reload, navigate away, or blindly repeat a task: check its recorded
ID first.

## Rehearsal verification

**Current baseline:**
- The solution builds with zero warnings.
- The offline suite runs the stage question (B18) against the real seed.
- All 21 code-tour bookmark anchors must resolve uniquely
  (`DemoRehearsalTests`).

**Live rehearsal (13 October 2026; earlier test and bookmark counts)**

Data explorer:
- All four systems loaded in both modes for both identities.
- Jane's records excluded Contoso and Fabrikam.
- Admin's After support view showed all three Contoso security chunks.

Model and API:
- Live validation found that the default reasoning deployment rejects
  tools through Chat Completions. Agent Framework therefore uses the
  Responses adapter, with an approved, narrowly scoped experimental SDK
  opt-in. No fake model or embedding fallback was used.

Chat:
- Before (admin) cited `support-005`.
- After (admin) cited all three `optimized-sup-005a/b/c` concepts.
- Jane reported no accessible Contoso review and got only authorized EU
  sources.
- Each summary request took about 9–11 seconds.

Follow-up task and feedback:
- The browser created Contoso task **2015**. It was verified against the
  SQL read endpoint.
- Feedback was verified in Cosmos.
- Both remain persisted; do not repeat the request to reset the demo.

Risk questions:
- The After risk question invoked `get_customer_health` and returned
  Jane's three accounts, with Betabit at high risk.
- A Before task request failed and left the task count unchanged.

These outcomes validate capabilities and evidence. They do not guarantee
identical wording on the day.
