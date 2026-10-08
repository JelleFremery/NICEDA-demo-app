# Agent use cases

`// NICEDA-PRACTICE-2: Identify agent use cases`

Written *before* the semantic layer and tools, so the schema serves the
use cases instead of the other way around. These three are what the
agentic chat demo (~slide 49) is built to answer well on the "After" data
and poorly (or not at all) on the "Before" data:

1. **"Which customers are at risk and why?"** — requires reading signal
   that today is scattered across CRM free-text notes, Cosmos support
   tickets with no severity field, and SQL contract status codes with no
   business meaning attached. Drives `get_customer_health` (practice 8)
   and the semantic layer (practice 3).
2. **"What's the history/context for customer X before I act?"** —
   requires retrieving the *right* chunks (not a 50-page blob, not an
   unrelated document) with security/classification respected. Drives
   `search_knowledge` (RAG, practices 5 and 7).
3. **"Create a follow-up task for the account owner."** — the one action
   an agent is allowed to take, and it must be auditable/governed rather
   than a raw database write. Drives `create_follow_up_task` (MCP,
   practice 8/CQRS write side).

Non-goals (explicitly out of scope for the demo, to keep it to three
tools): bulk data edits, deleting records live (practice 6 is demonstrated
via a documented command, not a live destructive action), cross-customer
analytics.
