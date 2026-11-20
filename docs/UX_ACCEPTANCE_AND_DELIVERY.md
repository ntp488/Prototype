# UX Acceptance, Agent Delivery and Definition of Done

> This is a verifiable implementation contract derived from [PRODUCT_INTENT.md](PRODUCT_INTENT.md). In any approved corporate integration, use fictional or approved test fixtures for screenshots and automated tests. Never push proprietary material to this public repository.

## 1. Overall success test

Give an engineer and a supervisor an authorized test scenario. They must be able to navigate in both directions between a site-level process map, a filtered fleet table, a selected repair visit, typed action items, a workspace work queue, and the module-specific operation—**without losing contextual selections**. Completing the valid module operation must automatically update the dependent action, workflow wait, traceability record, workspace rollup, and Atlas projection. Merely adding working APIs behind the legacy UI is a failure.

## 2. Interaction acceptance matrix

| ID | Area | User action | Observable acceptance |
|---|---|---|---|
| UX-01 | Global shell | Navigate to any major screen | Same design tokens, navigation, context header, form controls and status semantics; no legacy shell nested inside Nodal |
| UX-02 | Universal Gate 1 | Register an engine from an unknown family using demo data | Same common intake definition appears; family branches only if explicit eligibility rules match |
| UX-03 | Process design | Drill site -> gate -> child workflow | Breadcrumbs, discoverable dependencies, typed nodes; edits allowed only in authorized design view |
| UX-04 | Fleet graph | Pan/zoom or drag a node | Smooth motion; connection wires track node; personal positions persist; process topology remains unchanged |
| UX-05 | Process selection | Click a node | Full-height table scrolls into view filtered to visits **currently at** that process; historical completion is not conflated |
| UX-06 | Handoff analysis | Click a connection | Table shows transition/handoff population and semantics, including in-flight vs completed |
| UX-07 | Engine focus | Click a table row | View returns to graph, animates focus to **all currently active branches** for that engine, highlights lineage |
| UX-08 | Table controls | Search, filter, sort, clear selected process | Table updates promptly; clearing node slice does not erase unrelated manual filters |
| UX-09 | Deep graph | Double-click node / breadcrumbs | Child process displayed with independent layout, zoom and saved state |
| UX-10 | Multi-select | Select multiple processes | Union/intersection population matches known test visits and displays an unambiguous mode label |
| UX-11 | Workspace | Select parent workspace | Unique action IDs roll up once; owned, participating and downstream blocking counts are distinguishable |
| UX-12 | My Work | Select a module-controlled action | Opens the correct workbench with engine/visit/finding/action preselected |
| UX-13 | Context Lens | Open from an engine, finding, part or action | Shows related identity, why it is blocked or relevant, linked work, and permitted next action |
| UX-14 | Engineering | Record a valid simulated engineering decision | Engineering task resolves, Quality task/wait appears if required; part hold remains pending separate clearance |
| UX-15 | Inspection | Try to close inspection action manually | Refused as substitute for authoritative Quality result; a real module event can resolve it |
| UX-16 | Part tracking | Attempt move on held part | Operation rejected, no false move recorded; audit trail notes the attempt |
| UX-17 | Navigation | From workbench click Locate in Atlas, then return | Same visit/process context restored; previous graph/table state is preserved appropriately |
| UX-18 | Live vs history | Scrub replay, then switch live | Historical snapshot clearly labelled; live and forecast states not mixed |
| UX-19 | Scenario sandbox | Adjust staffing/arrivals in hypothetical scenario | Forecast results change but actual operational records and approvals remain untouched |
| UX-20 | Accessibility | Keyboard-only and reduced-motion testing | Core flows operable, focus visible, motion respects preference, controls labelled |
| UX-21 | Small screens | Use equivalent graph/table/workbench flow | No clipped critical actions or unusable horizontal overflow; touch controls accessible |
| UX-22 | Error handling | Expired permission, stale event, missing record | Recoverable explanatory state; no silent success, skipped approval or invented data |
| UX-23 | Workflow durability | Restart while waiting for inspection | Execution and linked action remain pending; valid correlated event resumes exactly once |
| UX-24 | Performance | Load a representative large synthetic fleet | Pan/zoom responsive; node summaries and table data agree; no unnecessary UI rerenders on one-second updates |

## 3. Visual review contract

Compare against the current Fleet Atlas, not arbitrary generic dashboard kits. Capture before/after screenshots for:
- App shell and global navigation;
- Fleet Site scope, Workspace scope, Engine focus;
- full-height process graph and corresponding full-height table;
- My Work and an action detail;
- workspace dashboard, parent rollup and dependency drilldown;
- Findings workbench with Operational Context Lens;
- Parts/scan and Intake workbench;
- 1440 px and phone-width layouts;
- dropdown open/focus states, empty state, loading state and error state.

A reviewer must inspect real browser renders, not infer visual quality from successful compilation. Smoothness and legibility are requirements: no cramped node cards, duplicate title bars, forgotten old buttons, or inconsistent selects.

## 4. Agent execution rules

1. **Inspect before changing.** Inventory actual routes, components, contracts and permissions, inside the target environment.
2. **Present a replacement matrix.** Every retained legacy UI component needs a reason; preserve domain rules but do not assume old presentation is valuable.
3. **Design the shell first.** Define interaction patterns and reusable components; implement a polished vertical slice before broad migration.
4. **Show browser evidence early.** Do not spend the entire iteration on backend APIs and postpone UX until the end.
5. **Use source-owned commands.** No direct database updates across module boundaries; no fake inspector sign-offs.
6. **Separate synthetic fixtures from production.** Test data must not contaminate real work records, telemetry or dashboards.
7. **Build and test after each meaningful change.** Fix errors before expanding scope.
8. **Never claim a feature is production-ready based on static checks.** State actual compilation, browser, integration and authorization evidence.
9. **Preserve state across navigation.** Use typed context and explicit URL semantics; avoid fragile global mutable UI shortcuts.
10. **Stop on uncertainty in regulated rules.** Escalate missing domain policy rather than making it up.

## 5. Required implementation deliverables

For each migration increment, produce:
- An inventory of affected old screens, modules and services, and which are retained/replaced.
- A context-navigation flow diagram and changed component contracts.
- Source changes with reproducible build/test commands.
- A visual walkthrough / screenshots demonstrating Atlas-level UX quality.
- An end-to-end scenario recording: action selected -> workbench operation -> confirmed event -> updated workflow/workspaces/Atlas.
- Automated tests for new state transitions and cross-module boundaries.
- A list of known limitations, assumptions and any steps not verified.
- Security/regulatory review items identified separately from functional UX completion.

## 6. Milestone gates

### Gate A — UX shell accepted
All major routes use common navigation, tokens, context and controls; initial screen review shows no nested legacy shell.

### Gate B — End-to-end navigation accepted
Graph -> table -> engine -> action -> workbench -> graph round trip retains exact engine and visit context.

### Gate C — First authoritative module integration accepted
Findings workflow resolves only from an allowed module record, not task status editing. Quality remains a distinct waiting step where required.

### Gate D — Organization intelligence accepted
My Work and hierarchical workspace dashboards reflect confirmed events and avoid double counting.

### Gate E — Extended modules accepted
New site modules register versioned capabilities and can participate in reusable workflows with correct authorization, traceability and error behavior.

### Gate F — Operational safety/production readiness
This gate requires independent enterprise reviews and validation; it is **not** satisfied by the public demo. Includes identity/qualification, audit records, security/export review, reliable persistence, deployment, recovery, and procedure-specific compliance.

## 7. Use this as an agent brief

> You are not integrating a new workflow widget into an old application. You are **transforming its interaction architecture into Nodal**. The current public prototype establishes the visual and interaction quality bar; the target organization's existing authoritative services establish the business-behavior boundary. Replace legacy navigation and page structures where they conflict with the product intent. Build a new coherent shell first. Demonstrate one complete work journey across Fleet Atlas, action items, workspace and a domain workbench. Require browser screenshot review and actual tests; never declare completion merely because APIs call each other. Preserve security, traceability and required human decisions.

Implementation agents should cite this document and [PRODUCT_INTENT.md](PRODUCT_INTENT.md) in their change proposals and report which acceptance IDs were demonstrated versus not yet tested.
