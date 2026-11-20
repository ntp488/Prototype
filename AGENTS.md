# AGENTS.md — Nodal Implementation Instructions

This repository is a **public, synthetic prototype and interaction reference**. Read these documents **before editing product behavior or integrating this project into another application**:

1. [Product intent and experience North Star](docs/PRODUCT_INTENT.md)
2. [Implementation architecture and migration blueprint](docs/IMPLEMENTATION_ARCHITECTURE.md)
3. [UX acceptance and delivery criteria](docs/UX_ACCEPTANCE_AND_DELIVERY.md)

## Primary instruction

**Do not interpret Nodal as a backend workflow package to be inserted into an existing UI.** The user explicitly wants an application-wide UX transformation based on the Nodal node-graph interaction model, not just reuse of its domain logic.

When adapting an existing enterprise application, retain authoritative domain logic, data ownership, validated procedures, security and authorization; **intentionally replace legacy shells, menus, isolated dashboards and disconnected module page layouts** when they conflict with Nodal.

## Non-negotiable patterns

- Site process definitions must support all applicable engine families; **Gate 1 plant intake is universal** by default. Engine family, engine asset and repair visit are distinct identities.
- Design and execution are distinct: reusable/versioned process definitions, many actual executions, typed action items tied to responsible users/workspaces.
- Fleet Atlas is an interactive **read-only process-data lens**. Move node positions as a personal view; never change business workflow edges by dragging an analytics node.
- Preserve the **graph -> full-height slice table -> engine spotlight** interaction, with clearable independent search/filters and animated return navigation.
- Workspace dashboards roll up child work without unique-action double counting; ownership, participation, and blocking impact are separate.
- Workbench actions must preserve engine/visit/finding/action context; engineers complete work in module UI rather than manually duplicating action completion.
- Authoritative domain events—not cosmetic progress or task-status clicks—advance regulated/validated operations.
- Durable human waits and approvals are future architecture requirements; no workflow auto-signs inspections or bypasses authorized actors.
- All pages must meet the **Fleet Atlas level** of visual quality, accessibility, mobile use, polished controls and meaningful motion.
- No proprietary or controlled business content, real maintenance data, private source, keys, or credentials may be added to this public repository.

## Before implementing

1. Inspect the current code and map what exists to the three docs above.
2. For an external enterprise migration, inventory proprietary screens **only within the approved environment**.
3. Produce a KEEP DOMAIN / ADAPT / REPLACE UX / RETIRE matrix; don't implicitly preserve legacy UI.
4. Design/implement a unified shell and reusable navigation context before extending modules.
5. Pick a first complete vertical slice (recommended: Finding -> Engineering -> independent Quality review).
6. Define acceptance IDs from 'docs/UX_ACCEPTANCE_AND_DELIVERY.md' that the change will demonstrate.

## During implementation

- Work in small, testable increments. Run 'dotnet build' and applicable smoke tests when the SDK is available.
- Run browser-based desktop/mobile walkthroughs; inspect actual screenshots, including open/focused selects, before claiming visual parity.
- Keep engine/visit/process/action context through links and navigation.
- Preserve access controls, module ownership, record revisions and traceability.
- Prefer coherent component APIs and a shared visual language to one-off page CSS.
- Protect performance under larger process graphs and synthetic fleets.
- Keep demo simulations and forecasts clearly distinguished from authoritative data.

## Before declaring done

Explain the exact user journey that is now complete; list acceptance IDs tested and not tested. Include compilation and browser test evidence or explicitly state verification gaps. Never call the system flight/maintenance approved based on the prototype.

An agent that only implements backend integration while retaining the legacy UI **has not fulfilled the project intent**.
