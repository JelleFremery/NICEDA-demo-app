# NICEDA data inventory

`// NICEDA-PRACTICE-1: Build a data inventory`

Before any agent touches NICEDA Inc.'s data, here is what exists, who owns
it, and how agent-ready it is *today* (the "Before" state). This file is
the human-readable mirror of `NICEDA.ApiService/DataInventory/DataInventoryCatalog.cs`,
which the seeding and data-explorer code reference directly so the catalog
never drifts from the running app.

| System | Real or mocked | Contains | Owner | Classification | Agent potential (before) |
|---|---|---|---|---|---|
| CRM | Mocked (in-memory seed) | Customer accounts, contacts, deal notes | Sales | Confidential | Low — key facts (health, risk, renewal) are buried in free-text notes across dozens of fields. |
| Azure SQL (`niceda`, `raw` schema) | Real (SQL Server container) | Contracts, contract lines, statuses | Finance | Restricted | Low — over-normalized; answering "is this customer at risk" needs a 4-table join an agent would have to reinvent. |
| Cosmos DB (`raw-support` container) | Real (Cosmos DB emulator container) | Support tickets, meeting notes, security assessments | Support | Confidential | Low — no consistent shape between documents, no metadata envelope, can't be filtered by classification/region. |
| SharePoint ("Legal") | Mocked (in-memory seed) | Contracts (PDF stand-ins), compliance reports, policies | Legal | Restricted | Low — only a folder path organizes anything; no tags/metadata to filter or retrieve by meaning. |

## Customers referenced across all four systems

So the "before vs. after" demo can show the *same* customer looking
sensible in one system and unreadable in another, these customers appear
in CRM, SQL, Cosmos, and SharePoint seed data:

- **Betabit**
- **Fenetre**
- **Techorama**
- Contoso
- Fabrikam

## Status

Catalog is implemented in code (`DataInventoryCatalog.cs`) and exposed via
the data-explorer API/UI so the "Before" demo beat (slide 10) can show
this table live instead of as a slide.
