# Nodal: Product Intent and Experience North Star

> **Status:** Target design and implementation intent, not a claim that every feature is production-ready.
>
> **Audience:** Software engineers, design engineers, AI coding agents, reviewers, and integrators adapting Nodal to a separate existing business application.
>
> **Public-repo boundary:** All examples are fictional. Never copy company source code, internal operating procedures, genuine maintenance records, credentials, or controlled technical information into this repository.

## 0. The one-sentence requirement

**Transform an existing operations web application into a cohesive, process-native working environment built around Nodal's interactive graphs, action items, hierarchical workspaces, and purpose-built module workbenches—while preserving validated domain services and data boundaries.**

This is **not** a request to add a workflow engine, insert Fleet Atlas into an existing page, or repaint the legacy navigation. It is a deliberate redesign of the application's primary interaction model.

**Target UX authority:** the current Nodal public prototype, especially its Fleet Atlas node graph, smooth graph-to-table-to-graph drilldowns, dark surfaces, precise operational density, animated transitions, and persistent context.

**Business logic authority:** the target application, after a separately approved internal inventory. Existing business rules, validations, data contracts, authorization, record ownership, and external dependencies must be maintained unless explicitly redesigned and approved.

## 1. Problem to solve

Conventional manufacturing applications isolate work by module, legacy screen, table, and organizational silo. An engineer completes an operation in one system but may also have to update a task, alert a coworker, reconcile a dashboard, and infer what depends on the result. Managers see disconnected summaries; individual engineers see disconnected screens.

The desired product makes that coordination intrinsic. Every material operation can be related to a physical asset, a repair visit, a process execution, the responsible workspace, a typed action item, and an audit/event chain. The system explains the current state of work, why that state exists, who can resolve it, and what happens next.

**Nodal becomes the interaction architecture and orchestration layer for operations, not a new dashboard bolted onto old software.**

## 2. Non-negotiable design principles

1. **Graph-first without graph-everywhere.** The interactive process graph is a first-class navigational and analytical surface available from any appropriate context. Detailed measurements, technical evidence review, and controlled forms still live in focused module workbenches.
2. **Process definitions are independent of engine family.** The common process library models the range of potential site operations. Gate 1 / plant intake is a shared entry process for any received engine; configuration and work scope introduce explicit conditional branches where applicable. Do not multiply the entire site map per engine family.
3. **Process definition != process execution != action item.** A reusable, versioned process can have many concurrent executions for repair visits and parts. Action items represent responsibility and work; one execution may create multiple actions, and actions can span processes.
4. **Action items coordinate actual work.** RACI-like assignments to users and workspaces power My Work and dashboards. The authoritative module operation—not an arbitrary action-item status click—establishes completion of module-controlled work.
5. **Every perspective is a lens on shared facts.** Site process design, fleet-wide state, workspace dashboards, individual repair visits, personal work queues, and module workbenches must reference the same entities and transitions, not maintain divergent shadow copies.
6. **Workspace hierarchy and process hierarchy are different.** A workspace represents organizational responsibility; a process represents what can happen; a cell/location represents where physical work occurs. These dimensions intersect without becoming one tree.
7. **Traceability is navigable.** Engine/part/visit/action/process/event relationships support an explainable digital thread and links back to authoritative source records.
8. **Animations communicate state.** Motion can indicate real or synthetic transitions, current location, selections, bottlenecks, and navigation; decoration must not imply that unverified work or approvals occurred.
9. **Read-only analytics, editable design, controlled execution.** Fleet Atlas visual layout can be rearranged per user without changing workflows. Process Design is where permitted users edit definitions. Regulated work requires authoritative, qualified module actions.
10. **Aggressive UX modernization; conservative domain changes.** Retain correct business behavior while being willing to replace routes, components, page structure, and navigation patterns. Legacy screens are not a design specification.
11. **Visually coherent at every depth.** No page should feel like an old application's leftover form beside the Atlas. Shared typography, surface treatments, controls, interaction cadence, and navigation apply to all modules.
12. **Production claims must be honest.** This prototype is simulated. Its demonstration approval buttons, toy persistence, randomized progress, replay checkpoints, and forecast heuristics must not be described as compliant operational systems.

## 3. The experience model: six connected lenses

### A. Process Design — what *can* happen

A zoomable, editable, composable process map of site-wide operations. The root represents the repair journey; reusable subworkflows and module capabilities are nested within gates and process groups. Users can drill into child workflows and return through breadcrumbs. Definitions are family-agnostic by default, with explicit configuration/eligibility predicates and versioned publications.

A reusable workflow is itself a node inside a parent workflow. Dependency edges, handoff events, human waits, and conditional branches remain visible at the appropriate level.

### B. Fleet Atlas — what *is* happening across the site

This is the flagship experience, not a decorative overview. It is a read-only process graph of current/historical fleet activity, with *movable nodes* that change only a viewer's layout. It supports pan, zoom, minimap, search, saved layouts, scope switching, animated process handoffs, heatmaps, real-time-style updates, historical replay, and engine spotlighting.

The graph and the fleet drilldown table are each immersive, viewport-height surfaces in a shared scroll journey:
- Click a process node to select a precise slice (default: visits **currently at** that process, not every visit ever recorded there), then smoothly scroll down to the full-height, manually searchable/filterable table.
- Click a handoff edge to inspect its upstream/downstream transfer population, with explicitly defined handoff semantics.
- Click an engine row to return to the graph with a camera transition and a highlighted engine digital thread; multiple simultaneous active branches must be supported.
- Clear the process selection without inadvertently clearing unrelated manual search or table filters.
- Multi-select processes for union/intersection analysis; drill down to deeper subprocesses without losing context.

Live-looking demo animation must never imply that a simulated or actual regulated approval happened automatically.

### C. Engine / Repair Visit Execution — one physical history

Choose an actual engine *instance* and a particular repair visit, not an engine family population. View its current and prior process executions, components, locations, work order, open findings, holds, dependencies, action items, and correlated events. Support parallel activities—quality, engineering, materials, and physical work can proceed concurrently.

A serialized part should retain time-aware relationships with parent assemblies and engines even after removal/installation elsewhere.

### D. My Work — daily execution front door

A role- and user-aware personalized work center. Shows owned actions, workspace-assigned actions, watched/informed actions, overdue/blocked work, and module-specific next steps. It opens the *actual workbench operation* with context preselected, not merely a status-edit form.

General coordination actions may be completed manually where allowed. Module-controlled engineering disposition, inspection, part movement, and release-related tasks resolve only through validated authoritative events.

### E. Workspace Intelligence — organizational responsibility

Workspaces can have parent/child relationships. They can own or participate in actions through RACI-like roles; parent dashboards roll up unique action identities and child workspace impact without double-counting shared items. Distinguish **owned**, **contributing**, and **blocking downstream** views. Each workspace has a process-aware dashboard with bottlenecks, waiting dependencies, queues, and direct Atlas drilldowns.

The workspace organizational hierarchy does not redefine physical cell locations or process topology.

### F. Module Workbenches — where real work is done

Findings/Engineering, Parts/Barcode, Gate 1 Intake, Teardown, Rebuild, Quality/Inspection, and Engine Test provide specialized, efficient work surfaces. Users can reach them from My Work, engine execution, workspace dashboards, or Atlas; the selected context follows them.

Each workbench includes a reusable **Operational Context Lens** with:
- What object is selected? engine, visit, part, finding, action, process.
- Why does this matter? related upstream causes, waiting process executions, downstream consequences.
- What actions are available? permissions and module-provided capabilities, with a safe route to the required authoritative workbench.

An inspection or sign-off may be requested and awaited by a workflow, but the qualified human judgment and official sign-off must be performed through validated domain processes.

## 4. Primary interaction journeys

### Journey 1: Fleet bottleneck -> engineering work -> fleet recovery

1. A supervisor sees a high-pressure process in Fleet Atlas.
2. Selecting its node animates to the full-height table filtered to *currently affected* visits.
3. Selecting an engine returns to the graph, spotlighting its active branch(es) and upstream blockers.
4. Opening related action items identifies the responsible user/workspace.
5. An engineer enters the Findings workbench with engine, repair visit, finding, and action preselected.
6. The engineer performs a validated disposition; its authoritative module records the outcome/evidence.
7. The typed engineering action resolves, a separate Quality request is produced where required, and the workflow enters the next durable wait.
8. Workspace queues, event history, engine execution, and Atlas projections update from the same confirmed events. Holds remain until their independent release conditions are met.
9. “Locate in Atlas” returns to the previous graph viewport/filters and highlights the affected engine.

### Journey 2: Receive an engine of a previously unseen family

1. Intake registers a fictional new engine/visit and creates the Gate 1 action.
2. The engine enters the **same** universal plant-intake process definition.
3. Applicable configuration checks select permitted family/scope branches, not a cloned site map.
4. Intake completion comes from its module event; the related action and downstream workflow state update.
5. Fleet Atlas and parent workspace dashboards automatically include the visit.

### Journey 3: Serialized part removal -> movement -> finding -> disposition

1. A teardown module records removal of a serialized component from a specific engine visit.
2. The component's historic parent/engine relationship is preserved; current location is updated only on confirmed movement.
3. A finding is reported on the part; a hold and typed engineering action are created.
4. A scan attempting an ineligible movement is rejected and audited.
5. The engine process waits for approved disposition/inspection evidence, without relying on a permanently open browser tab.
6. Rebuild/readiness projections reflect the unresolved dependency; relevant workspaces see its effects.

### Journey 4: Managerial rollup and time travel

1. A manager selects a workspace or parent workspace scope.
2. The graph and drilldown table reflect owned work and separately linked downstream impact.
3. A queue can be explained through evidence-backed causal categories (overlapping blockers allowed).
4. An investigation saves viewpoint, selected processes, filters, and scope.
5. Time replay shows past recorded/sampled state distinctly from live operation, while scenario forecasts remain isolated from real records.

## 5. Distinctive visual and interaction requirements

- Fleet Atlas is the **quality bar** for every page: restrained dark navy/charcoal surfaces, teal/cyan emphasis, crisp typography, compact information density, highly legible controls, refined micro-motion and responsive composition.
- Prefer one coherent controls panel rather than nested duplicate banners; give the graph space to breathe. Stat cards show clearly labeled numeric metrics without redundant explanatory captions.
- Dropdowns, menus, inputs, popovers, action sheets, tables, and focus states share a deliberate design system. Accessibility, keyboard navigation, mobile touch targets, and reduced-motion behavior are mandatory.
- Node positions in analytics can be personally rearranged, retained per drilldown level, and reset without altering process definitions.
- Pan/zoom, double-click drilldown, breadcrumbs, minimap, direct search, engine spotlights, edge pulses, and scope transitions feel like a single exploratory environment.
- Keep the path from site -> workspace -> visit -> action -> workbench -> Atlas reversible. Preserve context via typed navigation state and safe URL parameters.
- Avoid two disconnected competing systems: an old app navigation scheme and a new Nodal area.
- The module workbench should not become a graph diagram instead of an efficient tool. The graph explains context, while well-designed work forms support detailed decisions.

## 6. Anti-goals / explicitly unacceptable outcomes

The transformation is **not complete** if:
- Fleet Atlas exists only as a standalone page or iframe inside the legacy shell.
- Backend logic is integrated but existing site navigation, dashboards, tables, and workbenches mostly remain unchanged.
- The old UI receives new CSS colors but retains its disconnected interaction flow.
- Users must reselect the same engine/finding/action upon every module switch.
- A workflow node writes directly to another module's authoritative database, bypassing its APIs and policies.
- Action status changes impersonate an inspection/sign-off/engineering disposition.
- Process state is inferred solely from decorative progress percentages.
- A child workspace's actions are double-counted as separate site actions.
- The engine model is conflated with a single physical engine or repair visit.
- A read-only Fleet node drag alters underlying process topology.
- The implementation agent reports success based only on compilation or API tests without exercising and visually reviewing end-to-end UX.

## 7. What this public repository does and does not represent

The Nodal prototype is a demonstrator and **reference implementation of the intended interaction philosophy**, not a verified production system. In particular, the current general-purpose graph runner is bounded and synchronous; some long-running waits are implemented in synthetic domain scenarios. Persistent enterprise orchestration, hardened permissions, transactional event delivery, regulated recordkeeping, and validated forecast models are future engineering requirements.

The target proprietary application's actual contracts and infrastructure must be inventoried **inside its approved environment**. This public repository deliberately supplies only synthetic examples and generic target contracts. It is not necessary or permissible to publish internal source, genuine engine histories, technical drawings, regulated content, or company credentials in order to implement the UX transformation.

## 8. Reading order for implementers

1. This document — experience intent and non-negotiable requirements.
2. [Implementation Architecture and Migration](IMPLEMENTATION_ARCHITECTURE.md) — how to integrate safely.
3. [UX Acceptance and Delivery](UX_ACCEPTANCE_AND_DELIVERY.md) — observable completion criteria and phases.
4. Root [AGENTS.md](../AGENTS.md) — instructions for repository-based coding agents.
5. The existing Blazor screens and services, especially ProcessExplorer, FleetProcessDashboard, WorkCenter, WorkspaceCommand, ActionDetail, FindingsWorkbench, and OperationalCoordinationService.

**If a migration tradeoff is ambiguous, preserve authoritative domain rules, but favor replacing inherited presentation patterns in service of the Nodal experience.**
