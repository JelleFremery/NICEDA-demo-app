# Visual Studio demo bookmarks

The installer adds **actual native Visual Studio bookmarks** to the open
NICEDA solution, without removing existing bookmarks or changing source
files. It resolves unique text anchors, so line movement does not silently
send the code tour to the wrong place.

From **Windows PowerShell 5.1**, with `NICEDA.sln` open:

```powershell
.\tools\Set-DemoBookmarks.ps1
```

For Visual Studio 2022:

```powershell
.\tools\Set-DemoBookmarks.ps1 -ProgId VisualStudio.DTE.17.0
```

The default targets the installed Visual Studio 2026 (`18.0`). It does
not launch an IDE or switch a different open solution.

Open **View > Bookmark Window**, normally `Ctrl+K, Ctrl+W`. Next/previous
bookmark are normally `Ctrl+K, Ctrl+N` / `Ctrl+K, Ctrl+P`. Keyboard
bindings can differ by profile. The installer assigns the native names
**B01 - Inventory before architecture**, **B02 - Three use cases before
schemas**, and so on using `tools\demo-bookmarks.json`. It also removes
duplicate demo markers from the original installer. The Bookmark Window
is searchable/scannable by these descriptive names; grouping into folders
remains optional.

| Label | Slide/demo | File | Show this point |
|---|---|---|---|
| B01 | 12, Before | `NICEDA.ApiService\DataInventory\DataInventoryCatalog.cs` | Inventory ownership, classification, agent potential. |
| B02 | 12, Before | `docs\agent-use-cases.md` | Questions and action define the data requirements. |
| B03 | 12, Before | `NICEDA.ApiService\Sql\RawSqlSeeder.cs` | Normalized tables and raw status codes. |
| B04 | 43, After | `NICEDA.ApiService\Sql\OptimizedSqlSeeder.cs` | Business-named view over the original SQL rows. |
| B05 | 43, After | `NICEDA.ApiService\SupportDocs\OptimizedSupportSeeder.cs` | Metadata envelope and concept-based chunks. |
| B06 | 43, After | `NICEDA.ApiService\Rag\KnowledgeSearchService.cs` | Identity filters sources before embedding, and candidates before ranking. Scope-specific indexes prevent admin cache reuse for Jane. |
| B07 | 43, After | `NICEDA.ApiService\DataLifecycle\CustomerDataLifecycle.cs` | Mocks/vectors, original stores, feedback participate in deletion; startup reconciles durable markers. |
| B08 | 43, After | `NICEDA.Tests\RagKnowledgeSearchTests.cs` | Fixed expected source, not an LLM judging another LLM. |
| B09 | 49, Agentic | `NICEDA.ApiService\Agent\AgentChatService.cs` | Business read tool beside search and MCP orchestration; optimized health is an After-only capability. |
| B10 | 49, Agentic | `NICEDA.McpServer\Tools\CreateFollowUpTaskTool.cs` | Separate MCP command; request-context identity, not an identity argument. |
| B11 | 49, Agentic | `NICEDA.ApiService\Sql\FollowUpTaskCommands.cs` | Named, validated write capability, not arbitrary SQL. |
| B12 | 49, Agentic | `NICEDA.ApiService\Agent\AgentChatService.cs` | Feedback linked to interaction/customer. Open companion `AgentChatOutcomeRecorder.cs` to show how persistence failures warn without hiding confirmed actions. |
| B13 | backup, permission question | `NICEDA.ApiService\Security\DemoIdentity.cs` | Jane's explicit Betabit/Fenetre/Techorama scope. |

Run with an unlocked interactive desktop and the IDE available to receive
rename input. The script verifies native names and that **Next Bookmark**
can reach every inserted location. Re-running it is idempotent. All targets/labels are in
`tools\demo-bookmarks.json`.

Visual Studio owns persistence in its local solution state (normally
`.vs`/`.suo`, ignored by Git). Close the solution/IDE normally when you
want it to save that state; the installer does not close your session or
save unsaved source edits. Use the committed manifest/installer to
recreate bookmarks on another machine.
