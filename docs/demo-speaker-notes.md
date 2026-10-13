# NICEDA: PowerPoint demo notes

These are paste-ready notes for the three demo anchors in the supplied
deck: slide 12, slide 43, and the reserved slide 49. Each demo is five
minutes, not five minutes for every practice. The original deck has 44
content slides; slide 49 is a hidden placeholder, so unhide/replace it
before presenting.

Use the slide titles if the deck has been reordered. The deck's practice
numbers differ from the agreed ten-practice implementation; use
`docs/demo-plan.md` section 5 for the mapping. "Close the loop" is the
additional practice you specified, not a claim about the original deck.

## Before the audience arrives (not part of the demo clock)

1. Rotate the previously shared Foundry key and configure AppHost user
   secrets as described in `README.md`. Confirm the chat and embedding
   deployments are both available.
2. Start Docker. Build `NICEDA.sln` before launching the AppHost, then run
   `dotnet run --project NICEDA.AppHost --no-build`. Wait for seeding to
   complete. Do not cold-start the Cosmos emulator on stage.
3. Open the Web link from the Aspire dashboard. Check all four explorer
   tabs in Before and After. Confirm Betabit, Fenetre, Techorama, Contoso,
   and Fabrikam appear. This explorer is a presenter inventory, not a
   permission-filtered end-user screen.
4. Open Chat as **admin**. Submit "Summarize Contoso's onboarding blockers
   with sources." once in Before and once in After. This verifies model
   access and warms both embedding indexes. Inspect actual answers,
   citations, and traces; do not assume the model will always produce a
   worse Before answer.
5. Rehearse one authorized follow-up in After with a unique reason and an
   explicit future due date. Check persistence through the customer-health
   read model. Task creation changes data; do not repeat it merely to warm
   the model. Do not delete a seeded customer to reset the demo.
6. Run the normal tests. For live checks, follow the opt-in variables in
   `README.md` and use `--no-build` while the AppHost is running.
7. In the open NICEDA Visual Studio solution, run
   `.\tools\Set-DemoBookmarks.ps1` in Developer PowerShell. Open
   **View > Bookmark Window** (`Ctrl+K, Ctrl+W` in the default keyboard
   scheme). Use named locations in `docs/demo-bookmarks.md` to prepare
   your code tabs. Actual bookmarks are local Visual Studio state, not
   source-code comments.
8. Set a readable browser/editor font size. Keep the browser, Visual
   Studio, and the presentation adjacent. Begin on **Data explorer >
   Before > CRM**, with **admin** selected.

Do not display the dashboard's connection-string environment or user
secrets on the projector. The identity dropdown is a demo security
context, not authentication.

## Slide 12: "Agent systems begin with information architecture"

**Opening line:** "Before we build an agent, let's look at what it would
have to understand. This is ordinary enterprise data, optimized for the
people and systems that originally created it."

| Time | Action | Speaker notes |
| --- | --- | --- |
| 0:00-0:45 | Browser: Before > CRM. Point to Betabit, Fenetre, and Techorama; scroll horizontally. | "Sales can enter data quickly. For an agent, important meaning is scattered across dozens of fields and free-text notes. More columns do not equal more understanding." |
| 0:45-1:30 | Select SQL. Show status codes and contract/line fields. | "This is a real SQL Server container. Normalization is good for storage, but a business question still needs joins, code interpretation, and a decision about which fields matter. We have not separated the read use case from operational storage." |
| 1:30-2:15 | Select Cosmos DB; expand Raw API JSON. Contrast differently shaped records. | "This is a real Cosmos database. A ticket, meeting note, and assessment use different shapes. Missing consistent meaning and metadata makes reliable retrieval and filtering difficult." |
| 2:15-3:00 | Select SharePoint. Point at file/folder paths. | "Legal knows where to put a file, but folder names are not a policy or a machine-readable description. CRM and SharePoint are mocked; SQL and Cosmos are not." |
| 3:00-4:15 | Visual Studio: bookmarks B01, B02, B03. Show the inventory, use cases, and raw seed. | "First inventory the systems, owners, classifications, and agent potential. Then choose the questions and action we actually need. We are not handing the agent a database and hoping it works." |
| 4:15-5:00 | Return to browser, Before > CRM. | "The Before state isn't deliberately insecure. Permissions remain enforced. The contrast is understanding and retrieval quality, not an unsafe authorization bypass. Next we will reshape the same data." |

**Return to slides:** "Information architecture is part of agent
architecture."

**If short on time:** do the four browser tabs and show only B01. Do not
start chat in this first demo.

## Slide 43: "...that's the real challenge"

**Opening line:** "We haven't replaced the systems. We have made their
data usable for specific agent decisions."

| Time | Action | Speaker notes |
| --- | --- | --- |
| 0:00-0:45 | Browser: CRM, flip Before to After. | "The same customer now has a compact business profile. This is a projection over the original entry fields, not a second CRM." |
| 0:45-1:30 | Select SQL. Show customer health/risk and the available business fields. | "A named read model replaces repeated reasoning about normalized storage. The read side says what the business means; the write side exposes a validated command." |
| 1:30-2:15 | Select Cosmos DB. Show concept/source ID/classification/region/retention; find the Contoso security chunks. | "Classification is stamped during ingestion. We split by meaning: access controls, residency, incident response. A relevant answer can cite a specific source chunk rather than a giant blob." |
| 2:15-2:45 | Select SharePoint. Show classified Legal metadata. | "The legal file now has explicit customer, topic, and classification. Its original location is still traceable." |
| 2:45-3:45 | Visual Studio: B04 semantic view and B05 meaningful chunks/metadata. | "These are small, readable transformations. The view gives business vocabulary. The chunk envelope gives retrieval both meaning and policy context." |
| 3:45-4:30 | Visual Studio: B06 authorization and B07 deletion. | "Resolve identity before ranking; do not embed security fields. Deletion must also remove derivative data, including vectors and feedback. We show the deletion path in code, not by destroying our live seed." |
| 4:30-5:00 | Visual Studio: B08 deterministic retrieval test. | "We assert specific authorized facts and sources without asking an LLM to grade itself. The normal checks are offline and fast. We also have opt-in checks against real SQL, Cosmos, and MCP." |

**Return to slides:** "The optimization is not just adding embeddings.
It is adding meaning, policy, lifecycle, and testable capabilities."

**If short on time:** show SQL and Cosmos in the app, then only B04 and
B06. Keep deletion and tests as already-open backup tabs.

**Deletion detail if asked:** startup retries durable deletion markers
through the full lifecycle before seeding. The real-store regression uses
a scratch customer, fails after SQL commits, then verifies restart
reconciliation across original and derivative data. Do not delete a
seeded customer live.

## Slide 49: reserved agentic-access demo

Unhide/replace the placeholder and title it **"RAG for knowledge, MCP for
capabilities"**, echoing the deck's RAG/MCP distinction around slide 38.

**Opening line:** "Now we can ask an agent to use this data. Knowledge
retrieval and operational actions are different capabilities."

| Time | Action | Speaker notes |
| --- | --- | --- |
| 0:00-1:00 | Chat: admin, Before. Submit **"Summarize Contoso's onboarding blockers with sources."** | "The agent uses the poorly shaped knowledge. Look at which evidence it found and how useful the citations are, not just whether the prose sounds convincing." |
| 1:00-2:00 | Flip to After and submit the identical question. | "Same question, model, and identity; better-shaped knowledge. Point out meaningful source IDs/concepts in the citations and the search trace. This is a live comparison, not a promise that every model answer must be better." |
| 2:00-2:30 | After: select Jane; ask **"Summarize Contoso's onboarding blockers with sources."** | "Jane's scope is Betabit, Fenetre, and Techorama in the EU, not Contoso. The expected result is no authorized Contoso evidence or an explicit scope rejection. No data leak is the success condition." |
| 2:30-3:30 | Select admin again. Ask **"Create one follow-up task for Contoso about its onboarding blockers. Reason: Techorama live demo follow-up. Due date: [rehearsed future ISO date]."** | "This is the real, separate HTTP MCP service calling a validated command. Show the returned task ID and tool trace. A tool success means a persisted task, not that a person received an email. Before-mode writes are intentionally unavailable." |
| 3:30-3:50 | Click Helpful or Not helpful; enter a short comment. | "The feedback is structured data in Cosmos, linked to the interaction and affected customer. This demo records feedback; it does not automatically retrain the model." |
| 3:50-4:40 | Visual Studio: B09 Agent Framework orchestration, B10 MCP tool, B11 command validation. | "Two in-process tools handle knowledge search and customer health. One MCP tool handles the operational write. Identity comes from request context, not model-controlled tool arguments." |
| 4:40-5:00 | Visual Studio: B12 feedback; return to slides. | "The complete loop is governed evidence, governed action, and recorded outcome. Data readiness makes those capabilities understandable and testable." |

Replace `[rehearsed future ISO date]` in your notes before the talk.
Do not use a historical hard-coded date or rely on the model to guess it.
The first warm-up may take longer than later calls; budget the agent
requests as part of these five minutes.

**Important comparison boundary:** Before explicitly omits the
health-read tool; After exposes the optimized SQL view. Use the
source-based summary above for a same-capability retrieval comparison.
"Which customers are at risk?" also demonstrates different available
capabilities, not just a change in retrieval quality.
An empty/denied answer for Jane is not evidence that After retrieval is
broken; it is the permission boundary working.

**If short on time:** skip the extra Jane request and show B06 instead.
Prioritize one evidence-bearing After answer and one persisted MCP action.

## Honest fallback (all three demos)

If a service fails, show the explicit error and name the missing
dependency. Do not restart Docker or troubleshoot credentials on stage.
Use the prepared code tabs and the latest successful deterministic/live
test results instead. If you capture screenshots during rehearsal, label
them as rehearsal captures rather than live output.

Missing Foundry credentials intentionally return an unavailable response,
not a simulated answer. Feedback must be submitted before restarting the
API because interaction lookup is in memory; previously written feedback
remains durable in Cosmos.

Completed actions now appear with their task IDs even if outcome-feedback
persistence fails, accompanied by a warning. Retrying the same pending
request in the same chat view reuses its idempotency key; SQL returns the
existing task or rejects a changed payload. A new request/key represents
a new action, so do not reload/navigate away and blindly repeat a task
after an uncertain network outcome. Check its recorded ID first.
