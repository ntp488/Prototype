# Implementation Architecture & Migration Blueprint

> Read [PRODUCT_INTENT.md](PRODUCT_INTENT.md) first. This is a **target architecture**, not a representation that the current prototype is production-ready. This public documentation must never contain proprietary company code or controlled operational data.

## 1. Guiding decision

**Preserve existing business capabilities and their authoritative data; replace the inherited user experience when it conflicts with the Nodal interaction model.** Do not treat legacy menus, page structures, static dashboards, or module UX as requirements simply because they already exist. Keep validated rules, permissions, contracts, audit obligations, and specialized work inputs.

Nodal should become the application shell, navigational model, orchestration experience, and live process-intelligence lens. Detailed engineering work remains in specialized, usable module workbenches.

## 2. Target layers and boundaries

| Layer | Responsibility | Typical implementation |
|---|---|---|
| Nodal design system and shell | Global navigation, contextual breadcrumbs, responsive layout, focus/overlay behavior, accessible controls, persistent selections | Blazor components + shared CSS tokens |
| Process graph surfaces | Editable process design; read-only fleet/engine/workspace visualizations; pan/zoom, layout persistence, focus transitions | Graph renderer with JS interop |
| Work coordination | Typed operational actions, RACI, work queues, workspace ownership, overdue/blocked dependencies | Application services |
| Workflow orchestration | Published definition versions, durable executions, event correlation, human waits, idempotent retries | Independent background workers and durable storage |
| Module capability adapters | Queries, commands, events, UI entry points, permissions, schema versioning | .NET contracts and registered adapters |
| Authoritative modules | Findings, Quality, Parts, Intake, Teardown, Rebuild, Testing and future capabilities | Existing approved business implementations |
| Event and read projections | Correlated event ledger; fleet slices, hierarchy rollups, snapshots, causal dependency views | Transactional outbox + query projections |

**Do not implement long-lived waits with an open Blazor circuit or in-memory delay.** The current prototype's demonstration workflow runner is not the finished durable orchestrator.

## 3. Canonical data model

Separate:
- **Engine family/configuration** (eligibility) from **engine asset** (physical identity) from **repair visit** (one site journey).
- **Part asset** from **part location** and **effective-dated install/remove relationships**.
- **Workspace** (organizational responsibility) from **physical cell/location** from **site module** (capabilities) from **process** (what can occur).
- **Process definition + immutable published version** from **process execution + activity state** from **operational action item**.
- **Operational action** from **authoritative module record** (finding, approved engineering disposition, quality inspection, confirmed scan, test result).
- **Action assignment** (RACI responsibility) from **qualified authorization** (who may perform regulated operations).

Relationships are many-to-many where domain reality requires it: one engine visit may have concurrent engineering, quality, logistics, and rebuild branches; one blocked action may affect multiple dependent processes.

Each confirmed operation should be traceable through a stable event ID, event type, subject references, actor/principal, workspace/cell where appropriate, occurred/recorded timestamps, correlation and causation IDs, source record + revision, and evidence references. Preserve failed command attempts separately from confirmed completion.

## 4. Module capability contract

Every site module exposes these categories through a capability registry:

1. **Queries**: read authoritative state (finding, part, engine visit, inspection requirements, test record).
2. **Commands**: request validated operations (record a finding, request disposition, move part, complete eligible intake step).
3. **Events**: publish confirmed outcomes with correlation (finding reported, move confirmed, disposition recorded, inspection accepted).
4. **Workbench routing**: secure deep links into the relevant real UI.
5. **Policies and schemas**: input/output versions, access requirements, human-action requirements, side effects, idempotency and audit class.

An illustrative generic capability descriptor:

    module: findings
    capability: engineering.disposition.record
    kind: command
    requiresHuman: true
    permission: findings.disposition.record
    inputSchemaVersion: 1
    emits: engineering.disposition.recorded
    workbench: /modules/findings/{findingId}

This is NOT an authorization to fabricate such a proprietary endpoint or approve a disposition automatically. Real contracts must be discovered and approved *inside* the target's authorized environment.

**Golden rule:** a workflow may request an operation; the module validates and records it; the module's confirmed event advances dependent actions and processes. A workflow or action-item UI must never bypass authoritative validation.

## 5. Typed action items: coordination rather than an extra checklist

An operational action includes:
- identity, title, type, priority, due date, lifecycle state;
- owning workspace; responsible/accountable/consulted/informed user or workspace principals;
- engine, visit, part, finding, module record and process execution references;
- module capability and source record, if module-controlled;
- blockers and explicit completion predicate/authoritative event references;
- provenance and audit history.

General coordination actions can support permitted manual completion. Module-controlled actions resolve only when the source module confirms the required result. A recorded disposition can close the *engineering* action while creating a *separate Quality* action and wait; component hold remains until independently satisfied release criteria.

Workspace hierarchies aggregate distinct action IDs. Report **owned work**, **participation**, and **downstream blocking impact** as separate dimensions so cross-workspace contributions do not inflate unique site totals.

## 6. Process composition and engine-wide visibility

Gate 1 is site-wide universal intake for every received engine. Engine-family or configuration rules enter as *conditional subpaths*, not separate duplicate maps.

Workflow definitions may invoke other published workflow versions. Definition structure can drill down from site -> gate -> module process -> reusable workflow -> capability. Maintain separate edges for process dependencies, event handoffs, and organizational relationships.

Track **possible paths** independently from **observed actual executions**. The UI must acknowledge unknown/unmodeled actions instead of claiming to show every real-world possibility.

Fleet Atlas aggregates live process state over engine **visits**, not engine models, and reflects concurrent branches. A given node's default table slice means visits currently at that process; historical participation is a separately labelled mode.

## 7. Durable wait and regulated operation constraints

A waiting execution persists:
- immutable workflow version and context;
- activity and pending condition;
- correlation subscriptions and timeout;
- idempotency/lease and retry state;
- relevant action IDs and evidence references;
- observed result and audit trail.

Register waits and check existing authoritative state without missed-event races; use transactional outbox delivery, idempotent command handling, and dead-letter/manual recovery. Workflow replays and retries must never automatically repeat an inspection, human sign-off, or physical operation.

For regulated operations, a workflow can *request*, *route*, and *await* authorized inspection/sign-off. The qualified individual and approved domain system must determine and record the result. A green graph or completed action item is not proof of airworthiness.

## 8. Operational Context / Navigation contract

Make selected entities persist across navigation:
- site and workspace scope;
- engine asset + repair visit;
- part/finding/source record;
- process node + execution;
- action item and module;
- graph layout/camera and filters, where appropriate;
- originating page for reversible return.

Deep links should resolve these typed references with access checks. A user moves from Atlas -> process slice -> engine -> action -> domain workbench -> back to Atlas and returns to the original selection/viewport.

A reusable **Operational Context Lens** on relevant pages answers: *What is this? Why does it matter? What can this user do next?* The last part must be capability- and authorization-driven.

## 9. UX migration rules

The new shell is introduced **before** large module-by-module porting. Develop shared components, not page-by-page copies.

- Reuse Fleet Atlas typography, layout density, dark surfaces, teal accents, status semantics, dropdown and focus styling.
- Graph-first navigation where it adds value; fast focused forms where technical work demands it.
- Keep immersive process graph and full-viewport table with reciprocal scroll/spotlight transitions.
- Preserve independent table filters when a graph process filter is cleared.
- Personal graph dragging changes presentation coordinates, not definitions.
- Use minimap, search, breadcrumbs, saved investigations, scope switch and reduced-motion support.
- Make errors/loading/empty/stale states clear. UI must work with keyboard and on narrow screens.
- No legacy duplicate menu and no Atlas iframe.

## 10. Practical phased migration

**Phase 0 — Inventory and design authority.** Inside the approved proprietary environment, catalog routes, pages, components, services, use cases, permissions, record owners, and integration seams. Mark each UI element RETAIN DOMAIN / REPLACE UX / ADAPT / RETIRE. Capture screenshots of the current experience *internally*. Do not publish source/data.

**Phase 1 — Unified shell and design system.** Implement global context, navigation, layout tokens, shared dropdowns, workspace/engine selectors, deep-link routing, loading and error states. Replace the legacy shell rather than creating an island of Nodal pages.

**Phase 2 — Process/work views.** Mount Atlas, Process Design, Engine Execution, Work Center and Workspace Intelligence using trustworthy read projections. Ensure graph-to-table-to-engine journeys.

**Phase 3 — First vertical slice.** Choose Findings/Engineering. Deliver: Atlas bottleneck -> action -> workbench -> real authorized disposition -> confirmed event -> Quality task/wait -> refreshed Atlas and workspace dashboard. Demonstrate reversible navigation and durable state.

**Phase 4 — Extend domain adapters.** Add Parts/Barcode, Gate 1 Intake, Teardown, Rebuild and Testing through their approved modules. Process definitions gain reusable child workflows and event dependencies incrementally.

**Phase 5 — Production controls.** Versioned publishing, durable orchestration, authorization, audit/evidence linkage, observability, accessible UX, performance baselines, disaster recovery and regulatory validation before controlled deployment.

At the end of each phase: run builds/tests, browser visual comparisons, and explicit UX acceptance scenarios. Reject integration-only changes that preserve the legacy interaction structure.

## 11. Testing and review strategy

- Domain contract tests: no bypass of authoritative modules; permission gates; idempotent event handling.
- Rollup tests: one action counted once per scope while ownership/impact relationships remain distinct.
- Orchestration tests: waits across process restart; parallel branches; retries without repeated irreversible work.
- UX E2E: no loss of engine/action context while navigating; precise node/edge slicing; clearable filters; focus animation; saved layout restoration; keyboard/mobile behavior.
- Visual regression: compare each new page to the Atlas design language at representative desktop and mobile sizes.
- Performance: larger fleets and process maps should remain navigable; aggregate projections must avoid per-node per-engine N+1 reads.
- Traceability: a journey can be reconstructed from authoritative source records and correlated events.

See [UX_ACCEPTANCE_AND_DELIVERY.md](UX_ACCEPTANCE_AND_DELIVERY.md) for observable criteria.

## 12. Current public prototype starting points

- Graph + perspectives: 'Components/Pages/ProcessExplorer.razor', 'FleetProcessDashboard.razor'.
- Coordination UX: 'WorkCenter.razor', 'WorkspaceCommand.razor', 'ActionDetail.razor'.
- Module UX: 'FindingsWorkbench.razor', 'PartsWorkbench.razor', 'IntakeWorkbench.razor'.
- Services: 'OperationalCoordinationService.cs', 'ProcessOperationsStore.cs', 'OperationsStore.cs', 'ProcessMapStore.cs', 'FleetIntelligenceStore.cs', 'WorkflowRunner.cs'.

All paths above are relative to 'NodalWorkflowLab-Operations/'. They represent a **reference prototype**. Read the code before deciding which pieces to reuse; do not assume its simulation stores, role selector, sign-off buttons, or synchronous runner are production implementations.

**Implementation success means an employee's daily work genuinely happens inside the coherent Nodal experience—not merely that Nodal logic can call an old module's API.**
