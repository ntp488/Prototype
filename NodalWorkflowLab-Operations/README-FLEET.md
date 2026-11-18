# Nodal Operations — Full-Screen Fleet Drilldown

This is a complete **.NET 10 / Blazor Interactive Server** project, based on the previous movable Fleet Process Atlas release. It keeps the workflow designer, Process Design, Engine Execution, RACI/workspace demo, 219-example synthetic fleet, persistent simulation state, and 1-second randomized demo progression.

## Start locally

1. Stop the **previous** `dotnet run` instance with `Ctrl+C`, so HTTP port 5187 is available.
2. Extract this ZIP to a **new folder**. Don't overwrite a running deployment.
3. In PowerShell, from the ZIP's extracted root:

```powershell
cd .\NodalWorkflowLab-Operations
dotnet restore
dotnet build
dotnet run
```

4. Open **http://localhost:5187/processes** and choose **Fleet overview**.

If another process is using 5187:

```powershell
dotnet run --no-launch-profile --urls "http://127.0.0.1:5190"
```

Then open http://127.0.0.1:5190/processes.

## Full-screen interaction

**First screen — Live graph**
- The site-wide graph fills the available app content height, with KPI cards and filters above it.
- Drag a node's **MOVE** header to reposition it. Connections reroute; positions are saved per drilldown level in that browser. **Reset layout** restores the original layout.
- Pan the background; wheel to zoom; use Fit, +/- controls, and Explore steps to drill into subprocesses.
- **Click a process node** to select engines **currently at that process** (Ready, In progress, Waiting, or Blocked) and smoothly travel to the fleet table.
- **Click a connection** to see engines whose upstream step is Complete and downstream step is currently active/ready/waiting/blocked. This is a state-based handoff slice, not proof of a specific transfer event.
- **Explore fleet table ↓** takes you to the table without choosing a process.

**Second screen — Fleet visit explorer**
- The table fills a second viewport-height area with a sticky header, six operational summary tiles, and scrollable/paginated records.
- Clicking a node automatically applies an **At this step** filter. A distinct **All eligible** choice includes other eligible visits; **Complete / Active / Waiting / Blocked / Not started** isolate specific states.
- The removable **process filter chip** clears only the selected node/edge. It leaves your manual table search and global fleet filters intact.
- Search engine serial, repair visit ID, customer, work scope, model or location. Apply **engine family**, **exception**, and **sort order** filters independently of the graph.
- **Reset table filters** clears only manual table filters; the fleet-wide **Clear fleet filters** button clears the global scope filters.
- **Click an engine row** to smoothly return to the graph and animate/highlight the engine's current process location. The **Open visit** button still navigates to individual Engine Execution.
- On narrow screens, the table becomes touch-friendly cards rather than a horizontally inaccessible table.

## Simulated progress

The Live demo is on by default. Every **one second**, a randomized subset of eligible synthetic engine repair visits advances by a randomized percentage, with data persisted on the server. Controls let you pause and resume the simulation. The UI also refreshes every second when paused. Blocked work, part holds, engineering dispositions, inspection decisions, and sign-off gates **do not clear automatically**.

The data is fictitious. This is not a production maintenance, airworthiness, or compliance tool, and nothing is connected to Pratt & Whitney systems.

## Verify on your workstation

From the ZIP root:

```powershell
dotnet run --project .\NodalWorkflowLab-Operations-SmokeTests\NodalWorkflowLab.SmokeTests.csproj
```

Tests include expanded fleet data, blocked moves, durable waits, generic process topology, and the new current-step/handoff projection rules. Browser-based interaction checks verified movement, wire updates, saved node layouts, screen transitions, focus animation, manual searching, and mobile layout.

**Build limitation:** The creation environment does not have the .NET SDK, so the ZIP's .NET compilation and smoke-test execution still need to be verified with the commands above on a machine with .NET 10.

## Key source files

- `NodalWorkflowLab-Operations/Components/Pages/FleetProcessDashboard.razor` — two-screen UI and view-model filtering
- `NodalWorkflowLab-Operations/Models/FleetVisitSlicer.cs` — testable current-step vs. handoff semantics
- `NodalWorkflowLab-Operations/wwwroot/js/fleetGraph.js` — draggable nodes, pan/zoom, focus, and smooth scrolling
- `NodalWorkflowLab-Operations/wwwroot/css/fleet-dashboard.css` — full-height desktop and stacked mobile layouts
- `NodalWorkflowLab-Operations-SmokeTests/Program.cs` — simulator and slice smoke tests
- `NodalFleetTwoScreenPreview.html` — self-contained static visual test harness (not the running Blazor app)
