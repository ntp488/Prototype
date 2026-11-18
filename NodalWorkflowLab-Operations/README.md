# Nodal Workflow Lab — Process Intelligence

See `../START-HERE.md` for .NET 10 run instructions and a guided tour of all three process perspectives. `../RELEASE-NOTES.md` describes the new universal Gate 1 intake model, simulated receiving, updated UX, and validation limitations.

# Nodal Operations — Process Explorer iteration

The new process modeling and synthetic engine repair scenario are documented at the ZIP root:
**`START-HERE.md`** and **`IMPLEMENTATION_PLAN.md`**. Start the app with `dotnet run` and visit `/processes`.

---

# Nodal Operations — Aftermarket workflow prototype

A **runnable ASP.NET Core Blazor Web App (.NET 10, Interactive Server)** evolving the Blender-style node graph into a synthetic aftermarket operations workflow lab. **This is a prototype; it is not connected to Pratt & Whitney systems and contains no real engine, customer, employee or compliance data.**

## Requirements and run

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and extract the ZIP. In PowerShell:

```powershell
cd .\NodalWorkflowLab-Operations
dotnet restore
dotnet run
```

Open **http://localhost:5187**. The app also works with `dotnet watch run` and the included `run.bat` (double-click from the application folder). No third-party NuGet packages or database installation required.

### Compile and exercise the sample automation logic

From the **ZIP's top folder**, run:

```powershell
dotnet build .\NodalWorkflowLab-Operations\NodalWorkflowLab.csproj
dotnet run --project .\NodalWorkflowLab-Operations-SmokeTests\NodalWorkflowLab.SmokeTests.csproj
```

The standalone smoke checks verify seed graph validity, hierarchical rollups, blocked-item routing, RACI assignment, downstream follow-up creation, and workflow file persistence. **Packaging environment limitation:** the project source and JavaScript were statically checked here, but .NET compilation must happen on your installed SDK.

## What is implemented

### 1. Multi-workflow library

- Create, switch, rename, describe, duplicate, enable/disable and delete workflows.
- Workflow changes **automatically save on the server**; an explicit **Save** action is also available.
- **Import** a workflow JSON as a new, disabled workflow (never overwrites by ID), or import a legacy graph-only `nodes` / `edges` JSON. **Export** the selected workflow as JSON.
- The default library contains three runnable examples:
  1. **Blocked item escalation** — when an action becomes `Blocked`, route it to the `Repair Engineering` workspace, add a Responsible engineer, and emit a simulated notice.
  2. **Completion → quality review** — when an action becomes `Done`, create a new Quality action item referencing the original action.
  3. **Organization rollup snapshot** — a manual graph that reads and logs the recursive CEC dashboard totals.

### 2. Workspace hierarchy & dynamic dashboards

- Nested workspaces (including a sample CEC root, engine operations, induction, disassembly, assembly/test, engineering, quality, supply chain, materials, and programs).
- Create child workspaces from **Action items**.
- Every dashboard is **computed live from action-item relationships**: ownership, destination workspace, and workspace RACI assignments.
- Parent dashboards aggregate the complete descendant tree, deduplicating an action item within a given rollup. Items that cross workspace boundaries can legitimately appear on multiple workspace dashboards, but count once at the common parent.
- Live totals: open, in progress, blocked, done, completion percentage, direct association count, high-priority exceptions.

### 3. Action items and RACI-like assignment

- Create action items with title, synthetic engine reference, priority, and owning workspace.
- Change status from the action queue: Open, In Progress, Blocked, Done.
- Route an action item to a workspace without changing its original owner.
- Add/remove RACI-like `Responsible`, `Accountable`, `Consulted`, and `Informed` assignments to **users or workspaces**. The demo restricts each action to at most one `Accountable` assignment.
- Action item details serve as the **execution context** of workflow nodes.

### 4. Workflow nodes

| Node | Operation |
|---|---|
| Manual Trigger | Start a graph manually with a payload |
| Action Item Event | Entry point for `created`, `status-changed`, `assigned`, or any event; optional workspace scope |
| Load Action Item | Put the current action JSON into the payload |
| Check Action Status | True/false branch on status |
| Route to Workspace | Set action's assigned workspace |
| Set Action Status | Update action item status |
| Assign RACI | Add RACI assignment for user or workspace |
| Create Action Item | Create follow-up action in chosen workspace |
| Workspace Rollup | Emit JSON of aggregate workspace metrics |
| Notify (Simulation) | Log a notification without sending anything |
| Text Template / Transform / Condition / Wait / Log / Finish | Generic flow-control and debugging nodes |

All operations are explicitly implemented in C#. No user-authored scripts, arbitrary SQL, shell commands, or outbound HTTP are executed.

## Quick end-to-end demo

1. Run the app. Go to **Action items**. In the workspace tree, select **Engine Operations** (or the CEC root).
2. Find **ACT-1001 — Confirm incoming engine documentation** and set its status to **Blocked**.
3. The enabled **Blocked item escalation** workflow runs automatically. ACT-1001 is routed to **Repair Engineering** and receives a Responsible RACI assignment for **Jordan Lee**.
4. Open **Dashboards**, select **Repair Engineering**, and inspect its new assignment/rollup. Select **Columbus Engine Center** to see the organizational totals.
5. Return to **Action items** and set **ACT-1006 — Approve assembly readiness checklist** to **Done**. The **Completion → quality review** workflow creates an action in **Quality & Airworthiness**.
6. Go to **Graph editor**, choose a workflow from the left library, choose a **test action** in the right inspector, add/drag/connect nodes, and **Run workflow** or ▶ **Test this node**. Selecting **Organization rollup snapshot** runs a read-only graph that reports totals.
7. Refresh/restart the app. Saved graphs and sample operations changes remain. **Dashboards → Reset sample operations data** restores the demo actions and workspaces without deleting workflows.

## Data layout

Generated on the **first application run**, relative to the **application content root**:

- `App_Data/workflows.json` — all workflow documents/graphs.
- `App_Data/operations.json` — synthetic workspaces, users, action items, and RACI assignments.

Both repositories are singleton services protected by `SemaphoreSlim`, with atomic temporary-file replacement. The two files are shared across browser sessions on the same server process. They are deliberately stored on the **server**, not browser `localStorage`.

If you deploy in Docker, bind mount a durable host directory to `/app/App_Data`; otherwise container deletion loses changes. The current Dockerfile can serve this app, but persistence requires that volume mount and write permissions.

## Architecture

| File | Purpose |
|---|---|
| `Models/OperationsModels.cs` | Workspace tree, users, action items, RACI, rollup aggregation |
| `Models/WorkflowDocument.cs` | Workflow metadata and three seeded workflow templates |
| `Models/WorkflowGraph.cs`, `Models/NodeCatalog.cs` | Graph schema and type-safe node definitions |
| `Services/WorkflowLibraryStore.cs` | Server-side CRUD and persistence for workflow library |
| `Services/OperationsStore.cs` | Synthetic operations CRUD with file persistence |
| `Services/WorkflowRunner.cs` | Bounded, node-based execution with operations adapters |
| `Components/Pages/Home.razor` / `.razor.cs` | Interactive editor, operations UI, dashboard UI, event entry points |
| `wwwroot/js/nodalCanvas.js` | Client-only high-frequency drag, pan, zoom and SVG wire drawing |
| `NodalWorkflowLab-Operations-SmokeTests/` (sibling in ZIP) | Executable, dependency-free .NET smoke tests |

## Integration path for the real application

The UI intentionally uses placeholder identifiers; in a production aftermarket application, the `OperationsStore` should be replaced with **authorized application services** exposing commands/events for action items and workspace membership. Workflow nodes should invoke those service contracts, rather than directly querying tables or calling unknown modules.

Suggested next architectural slices:

1. **Enterprise identity and per-workspace authorization** using your real identity provider and server-side privilege checks for every graph edit, node execution and action modification. RACI is an assignment model, not itself an authorization rule.
2. **Transactional database** for versioned workflow definitions, workspace tree, action events, command idempotency, row-level concurrency and audit history. JSON files are not suitable for concurrent enterprise-scale writes.
3. **Durable event outbox + workflow worker** instead of in-circuit sequential event dispatch. Persist trigger delivery, retry state, workflow run instances, failure recovery and idempotency keys. Workflow-generated writes currently do **not** trigger secondary workflows (cycle prevention in this demo).
4. **Typed ports, module-specific nodes, versioned schemas, and declarative permissions**; evaluate published/approved graph versions, not untrusted arbitrary graph edits.
5. **Controlled data protections** appropriate to actual regulatory scope (including ITAR/EAR as applicable), least-privilege access, auditing, encryption, retention policies and software validation. **Do not put real customer, engine, export-controlled, or sensitive employee data in this prototype.**

### Limitations / intentional prototype behavior

- Automatic triggers currently originate from **actions performed in the Blazor app UI**, not an external message bus. The app dispatches those events to all enabled workflows within the current interaction.
- Workflow writes do **not** recursively generate further workflow triggers; this avoids unbounded loops for the demo.
- A follow-up node may create another review item if an action transitions to `Done` more than once; real deployments require idempotency.
- No login, multi-tenancy, global permission gates, approvals, schedules, notifications delivery, workflow version history or durable distributed queue.
- The test action chosen in the inspector controls which item an operations node modifies during a **manual run**. These are real changes to the synthetic demo data, not a dry-run sandbox. Use **Reset sample operations data** to restore seed state.
- This sample uses .NET 10 and built-in ASP.NET packages only, with no external dependency or API credentials.

## Browser JavaScript observer API

```js
const unsubscribe = NodeLab.on('node-complete', event => {
  console.log(event.detail.workflowId, event.detail.nodeId, event.detail.payload);
});
await NodeLab.run('blocked-check', 'test'); // uses inspector's current test action
console.log(await NodeLab.getGraph());
console.log(await NodeLab.getWorkflowId());
unsubscribe();
```

Browser events are observers only. The real workflow state, persistence, and execution stay on the server.
