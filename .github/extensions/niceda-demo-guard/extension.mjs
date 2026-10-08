// Extension: niceda-demo-guard
//
// Small demo-support extension for the NICEDA Techorama 2026 talk
// ("Optimizing data for agentic use"). It provides:
//   1. A tool that checks a data source description against the 10
//      agent-readiness practices tracked in docs/10-practices.md.
//   2. A safety hook that blocks obviously destructive database
//      operations before they reach a tool, mirroring the session's
//      "avoid unsafe autonomous actions" talking point.
//
// This is intentionally minimal scaffolding — extend it once the
// practices and demo flow are finalized.

import { joinSession } from "@github/copilot-sdk/extension";

const DESTRUCTIVE_SQL_PATTERN = /\b(drop|truncate|delete\s+from|alter\s+table|update\s+\w+\s+set)\b/i;

const session = await joinSession({
    tools: [
        {
            name: "niceda_check_agent_readiness",
            description:
                "Scores a short description of a data source (table, API, or document store) " +
                "against the NICEDA 10 agent-readiness practices and flags obvious gaps " +
                "(e.g. missing descriptions, overexposed schemas, no permission boundaries).",
            parameters: {
                type: "object",
                properties: {
                    sourceName: { type: "string", description: "Name of the data source being evaluated" },
                    description: {
                        type: "string",
                        description: "Free-text description of the data source: schema, access pattern, permissions, docs, etc.",
                    },
                },
                required: ["sourceName", "description"],
            },
            handler: async (args) => {
                const text = args.description.toLowerCase();
                const signals = [
                    { label: "Has human-readable field/column descriptions", hit: /descri(be|ption|ptive)/.test(text) },
                    { label: "Documents access permissions/boundaries", hit: /(permission|role|scope|boundary)/.test(text) },
                    { label: "Avoids exposing full raw schema", hit: !/select \*|all columns|entire schema/.test(text) },
                    { label: "Mentions governed/validated queries", hit: /(governed|validated|allow-?list|parameteri[sz]ed)/.test(text) },
                    { label: "Mentions retrieval strategy (RAG/MCP)", hit: /(rag|mcp|retrieval|vector|embedding)/.test(text) },
                ];
                const hits = signals.filter((s) => s.hit).length;
                const lines = signals.map((s) => `${s.hit ? "✅" : "⬜"} ${s.label}`);
                return [
                    `Agent-readiness scan for "${args.sourceName}": ${hits}/${signals.length} signals detected.`,
                    ...lines,
                    "",
                    "This is a heuristic scan for demo purposes — see docs/10-practices.md for the full checklist.",
                ].join("\n");
            },
        },
    ],
    hooks: {
        onPreToolUse: async (input) => {
            const argsText = JSON.stringify(input.toolArgs ?? "");
            if (DESTRUCTIVE_SQL_PATTERN.test(argsText)) {
                return {
                    permissionDecision: "ask",
                    permissionDecisionReason:
                        "niceda-demo-guard: this looks like a destructive SQL statement (DROP/TRUNCATE/DELETE/ALTER/UPDATE). " +
                        "Confirm this is intentional before an agent executes it against enterprise data.",
                };
            }
            return undefined;
        },
    },
});

void session;
