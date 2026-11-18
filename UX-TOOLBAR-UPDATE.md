# Nodal Operations — Unified Fleet Atlas Toolbar

## Changes

- Combined the two stacked Fleet Atlas control surfaces into **one unified toolbar**.
- Preserved all Site / Workspace / Engine scope options, conditional scope selectors, heatmap metric selector, blocker explanation, time machine, scenario simulation, saved views, and process search.
- Preserved **Refresh**, **Live demo / Pause demo**, **Register engine**, **Reset demo**, and its one-second synthetic status indicator. All original Blazor event handlers are unchanged.
- At desktop widths (1320px and above), shows a compact **single-row** toolbar with context-aware short labels where necessary. At smaller widths, it wraps groups within one panel; on narrow phone widths, all live-demo action buttons remain directly visible.
- The Fleet Atlas process graph gains additional vertical space. All other pages, data models, simulation logic, and workflow behaviors are preserved.

## Files changed

- `NodalWorkflowLab-Operations/Components/Pages/FleetProcessDashboard.razor`
- `NodalWorkflowLab-Operations/wwwroot/css/operations-shell.css`

## Run

From the extracted `NodalWorkflowLab-Operations` folder:

```powershell
dotnet clean
dotnet restore
dotnet build
dotnet run
```

Then open <http://localhost:5187/processes?mode=fleet>.

## Validation

- Inspected retained Blazor event bindings and Razor section markup.
- Browser-rendered fixture checked at 1920, 1440, 1024 and 390 px.
- Checked all groups exist, native selects remain present, buttons are visible, no document-level horizontal overflow.
- Build not run in this environment because `dotnet` is not installed; your local `dotnet build` is required.

All engine data remains synthetic; this is not an approved maintenance or regulatory workflow system.
