# Nodal Connected Operations — Implementation Notes

## Scope of this release

Extends the previous Fleet Process Intelligence Atlas with a daily work layer and module workbenches. Reuses the prior persistent `OperationsStore`, `ProcessOperationsStore`, `ProcessMapStore`, engine/part findings and Fleet Atlas rather than replacing them. Existing node graph editor, simulation, replay, investigations and site-wide process models are retained.

### New user experiences

- **My Work `/work`**: demonstration viewer switch, personal vs workspace queue, status/module/text filters, priority ordering, 10-second refresh, links to each authoritative workbench and context rail.
- **Workspace Intelligence `/workspaces`**: hierarchical workspace selection, deduplicated action rollups, activity and completion metrics, a compact process footprint and deep links into actions/Atlas.
- **Action detail `/actions/{id}`**: linked engine/finding/source capability, RACI assignment, persistent statuses and a module-controlled completion guard.
- **Site-module directory `/modules`**: implemented Findings, Parts and Intake workbenches; visible placeholders for later Teardown, Rebuild and Testing workbenches.
- **Findings `/modules/findings`**: synthetic Engineering accept/revise, independent Quality inspection wait, role-preview gating and automatic action/status propagation.
- **Parts `/modules/parts`**: part lookup and validated movement with a trace event even on rejection.
- **Intake `/modules/intake`**: universal Gate 1 progress and linked induction action completion.
- **Operational Context Lens**: reused side panel with engine/visit, source action, current stage, holds, waits, open findings, trace events and links.
- **Atlas integration**: visiting `/processes?mode=fleet&engine={id}` opens Fleet Atlas and focuses the requested engine; Atlas digital-thread inspector can open linked action detail. Engine execution panel links directly into module workbenches.
- **Design system**: consolidated Atlas-derived dark surfaces, accent buttons, compact typography, standardized nav, status chips and responsive layouts across new and legacy pages.

### Implementation / ownership boundaries

| Object | Authoritative demo owner | Concern |
|---|---|---|
| `ProcessFinding`, `ProcessRun`, `TrackedPart`, `ProcessAuditEvent` | `ProcessOperationsStore` | Source module operations, synthetic holds, persisted waits, historical events |
| `ActionItem`, workspace and RACI | `OperationsStore` | Responsible work, status, assignment, workspace dashboard |
| Process definitions | `ProcessMapStore` | Version-independent process hierarchy, cross-module stage relationships |
| Fleet graph | `FleetProcessDashboard` | Read-only visualization, focus, slices, scenario and replay |
| `OperationalView` + `OperationalCoordinationService` | Composed read model / command routing | Cross-module contextual navigation; does not duplicate source records |

Source-backed items are idempotently enriched with `ModuleId`, `CapabilityId`, `SourceRecordId` and `CompletionEventType`. Direct `Done` updates of those items are rejected in the action store; corresponding module commands update the action through `SetVerifiedStatusAsync`. In particular, Engineering disposition can create an independent Quality inspection work item **without clearing the held part**.

## UX decisions

1. Keep Fleet Atlas as the visual orientation layer, not the work-entry form.
2. Every module keeps its domain-specific controls and receives a shared context rail.
3. The daily Work Center prioritizes *one action, one owner, one next action*; the module owns the actual business decision.
4. Preserve the user's graph-to-table interactions and node editor. All new pages use the same top-level navigation.
5. Avoid a single hard-coded engine family in any generic intake logic.
6. Gracefully stack panels at phone widths. Visual previews were inspected at 1530px desktop and 430px mobile using Chromium.

## Next production milestones (not implemented)

- Enterprise authorization/SSO, qualified identity & delegated capability policies; proper segregation of duties. **Role selection in this demo is not security.**
- A transactional DB (SQL Server / PostgreSQL), outbox events, idempotent handlers, audited immutable source-event history and versioned business schemas.
- Real durable, versioned workflows with event subscriptions surviving code deployment, retry/compensation, and multi-instance concurrency.
- Maintenance-data governance, evidence retention, signature identity/evidence binding under approved applicable quality procedures, and validated inspection/release operations.
- End-to-end teardown/rebuild/test module workbenches, including equipment & station integration.
- Accurate causal lead times, historical attribution, more granular physical work and action ownership, and configurable workspace permissions.

**All seeded engines, serial numbers, technical observations, decisions, testing events and personnel are fictional.** No FAA signature, maintenance release or airworthiness determination is performed.
