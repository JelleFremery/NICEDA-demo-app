# NICEDA demo script (paste into PowerPoint speaker notes)

Everything under each "Slide" heading below is meant to be pasted into
that slide's notes pane as is. It is plain text on purpose: no tables,
no formatting that PowerPoint would show as raw symbols.

Background, the customer cast, prep, bookmarks and code explanations
are in docs/demo-briefing.md. Read that before the session.

---

## Slide 10 — NICEDA: Before

DEMO 5 MIN. Start: browser, Data explorer, Before, CRM, user = Admin.

OPEN: "Before we build an agent, let's look at what it would have to understand. Keep two questions in mind: is Betabit at risk? And: summarize Contoso's security review with sources."

[0:00] CRM: Betabit row. Scroll to status, contract value, sales notes.
SAY: "Is Betabit at risk? Status says 'Active, watch closely'. The real answer is in a sales note: champion left, new CTO, competing quote. Value is '~€180k/yr', next to 'about 60k EUR annually' and '$310,000/yr'. 'Renews in about six weeks' — six weeks from when?"

[0:50] SQL: Betabit StatusCode AtRisk, AnnualValue, EndDate.
SAY: "Real SQL Server. Here Betabit IS AtRisk — after a join and a code lookup. And the end date is a year out, while CRM says six weeks. Which source is right? An agent can't decide that; an owner has to."

[1:30] Cosmos DB: Betabit support-001 vs support-002, then Contoso support-005.
SAY: "Real Cosmos. Same customer, two ticket shapes: ticketId here, ticket_number with nested details there. And here is the answer to our Contoso question: one blob — access controls, residency, incident response. No concept, no classification, no region. An agent gets all of it, or a random slice."

[2:40] SharePoint: /Customers/Betabit/Compliance/Security_Questionnaire_Response.pdf
SAY: "Legal knows where to file things. But 'Compliance' in a folder path isn't a classification, an owner or a retention rule. CRM and SharePoint are mocked; SQL and Cosmos are real."

[3:10] Visual Studio: B01 (inventory). Mention B02.
SAY: "First step: inventory the systems, owners, classifications and agent potential. Then pick the three questions and one action we actually need. We don't hand an agent a database and hope."

[3:50] Browser, CRM: Fabrikam's account manager = Jane Okafor. Switch user to Jane: Fabrikam and Contoso disappear. Switch back to Admin.
SAY: "CRM says Jane manages Fabrikam, yet she can't see it. Her scope is an explicit EU customer list, not a free-text field. So Before isn't insecure — permissions are enforced. The problem is understanding. Next, we reshape the same data."

BACK TO SLIDES: "Information architecture is part of agent architecture."

SHORT ON TIME: skip SharePoint and B01. Keep Betabit CRM → SQL, the Contoso blob, and the Jane/Fabrikam switch. No chat in this demo.

---

## Slide 40 — NICEDA: After

DEMO 5 MIN. Rule: stay on one tab and flip Before ↔ After. Don't tour all systems again.

OPEN: "We haven't replaced a single system. We've made their data usable for specific agent decisions. Same two questions."

[0:00] CRM, Betabit. Flip Before → After.
SAY: "Same customer: nine curated fields, a health summary built from the notes, a classification, and SourceSystem/SourceId back to the original. A projection over the original entry, not a second CRM." (B15 if asked)

[0:40] SQL, flip → After. RiskLevel: Betabit High, Fenetre Low, Contoso Low.
SAY: "Is Betabit at risk? One row, in business words. Fenetre, which renewed, stays Low — the rule tells accounts apart. The view owns the rule: AtRisk means High, renewal within 90 days means Medium. The glossary tells the agent what RiskLevel means and to still check CRM and support evidence. The CRM-versus-SQL date conflict becomes an owner's decision, not an agent's guess."

[1:20] Cosmos DB, flip → After. Contoso optimized-sup-005a/b/c: concept, sourceId support-005, classification, region, retention. Then Betabit's envelope.
SAY: "Remember the blob? Now three chunks by meaning: access controls, residency, incident response. Each carries policy metadata stamped at ingestion, so an answer can cite exactly one. And Betabit's two ticket shapes now share one envelope."

[2:20] SharePoint, flip → After on the Betabit questionnaire.
SAY: "Now it has a concept, Restricted, an owner — Legal — and seven-year retention. The original path is still there for traceability."

[2:40] Visual Studio: B04 semantic view, B05 chunk envelope.
SAY: "Small, readable transformations. The view gives business vocabulary; the envelope gives retrieval meaning and policy."

[3:20] B06, then B17. One sentence on B07 (don't open).
SAY: "Identity filters the sources BEFORE anything is embedded or ranked. Policy fields filter the chunk but are never embedded. And deletion removes derivative data too: vectors, feedback, audit rows."

[4:00] B18: the stage-question test.
SAY: "This is the proof. The exact question I'll ask the agent is a unit test on the real seed. After must rank the three Contoso concepts first; Before only gets concept-less fragments; Jane never even embeds Contoso text. No LLM grades another LLM. Offline, milliseconds."

BACK TO SLIDES: "Optimization isn't just adding embeddings. It's adding meaning, policy, lifecycle and testable capabilities."

SHORT ON TIME: SQL and Cosmos only, then B06 and B18.

IF ASKED ABOUT DELETION: startup retries durable deletion markers through the full lifecycle; a real-store test uses a scratch customer. Never delete a seeded customer live.

---

## Slide 48 — NICEDA: Agentic use

DEMO 5 MIN. Start: Agent chat, user = Admin, Before. Due date for the task prompt: ____-__-__ (fill in at prep).

HERO PROMPT: Summarize Contoso's security review with sources, check Contoso's current account health, and recommend one follow-up action.
TASK PROMPT: Create one follow-up task for Contoso. Reason: Techorama live demo - add a dedicated Contoso escalation contact. Due date: [YYYY-MM-DD].

OPEN: "Now let's give an agent this data. Knowledge retrieval and operational actions are different capabilities, and we'll see both."

[0:00] Admin, Before: submit HERO PROMPT. Open the trace.
SAY: "Look at what the agent got: two 60-character fragments, no concept. And no health tool at all — Before has no governed view to expose. Judge the evidence, not how convincing the prose sounds." (B14 if asked)

[0:50] Flip to After; submit the identical HERO PROMPT.
SAY: "Same model, same identity, same question. Now the citations are 005a/b/c, get_customer_health returns Contoso as Low risk, and the recommendation follows from the incident-response finding: a dedicated escalation contact. A live comparison, not a promise every answer is better."

[1:50] Admin, After: submit TASK PROMPT.
SAY: "Each chat turn is a fresh run, so the action request is complete on its own. This is the real, separate HTTP MCP service calling a validated command. Here's the task ID. Success means a persisted task, not that someone got an email. In Before mode, the MCP tool refuses writes."

[2:40] Short comment + Helpful / Not helpful.
SAY: "Feedback is structured data in Cosmos, linked to the interaction and the customer. It does not retrain the model."

[3:00] Switch to Jane (After); submit HERO PROMPT. Show the trace. Switch back to Admin.
SAY: "Jane's scope doesn't include Contoso. Search finds no Contoso evidence — it was never embedded for her — and the health tool returns denied. Success means no leak. Unrelated EU citations are not Contoso evidence."

[3:40] Visual Studio: B19 After-only tool, B10 MCP tool, B20 audit hook.
SAY: "Capabilities are explicit: the health tool exists only in After. The write is a separate MCP service; identity comes from request context, never from a model-controlled argument. And every tool call — including that denial — goes through one audit hook."

[4:15] Data explorer, Audit log (Admin, newest first): Jane's denied get_customer_health row, Admin's create_follow_up_task row. Then Agent usage.
SAY: "Everything we just did is a row: mode, who the agent acted for, the customer, the outcome. Roll it up and slide 43's question answers itself: which data does the agent never touch? Nobody has asked the agent to create a task for Fenetre or Techorama. That's data to improve — or to stop maintaining."

SHORT ON TIME: skip feedback and B19. Keep one After answer with citations, one persisted MCP task, and Jane's denied row in the Audit log.

IF SOMETHING FAILS: show the error, name the missing dependency, don't troubleshoot on stage. Fall back to B18 (the stage question passes offline).
