# Nodal Connected Operations — Fleet Atlas UX Refinement

This update builds on **Nodal-Connected-Operations-WorkCenter-Blazor-Fixed.zip**.

## Fleet Atlas header

- Removed the redundant `FLEET INTELLIGENCE / SYNTHETIC LIVE DEMO` hero banner from the Fleet dashboard component.
- Expanded the existing Process Intelligence perspective header to present both sets of context in a single integrated header, retaining Process Design / Engine Execution / Fleet Overview navigation.
- Moved the refresh, live demo, engine registration, and reset buttons into a slim telemetry toolbar below the KPIs.
- Removed explanatory captions below all five Fleet Atlas KPI values, keeping their labels and totals intact.
- No process counts, workflow execution logic, simulation rules, or persisted operational data were modified.

## Site-wide dropdown component styling

- Applied the same `nodal-select` visual treatment to **all 44** native `<select>` controls across the Node Designer, Process Atlas, Work Center, action editor, and module workbenches.
- Created `wwwroot/css/select-system.css` (loaded **last** in `Components/App.razor`) with unified teal/blue borders, chevron, consistent hit targets, disabled styling, focus-visible rings, and native dark option menus (`color-scheme: dark`).
- Native selects are deliberately retained for reliable keyboard navigation, mobile picker behavior, and assistive-technology semantics. No third-party dependency or JavaScript combobox was introduced.
- Dropdown values and existing `@bind`/`@onchange` handlers are unchanged.

## UX checks performed

- Used Chromium at desktop (1440px) and mobile (390px) widths to validate a representative layout using the application's own stylesheets. Previews are **static style fixtures**, not screenshots of a running Blazor server.
- Tested focus and ArrowDown keyboard selection in Chromium.
- Verified the dark menu scheme and select sizing in both viewport sizes.
- Checked that every Razor select has the shared class, the five KPI captions are gone, and the old duplicate Fleet hero was removed.
- Checked JavaScript syntax and archive integrity.

## Verification limitation

The environment used to prepare the ZIP does not have the .NET SDK, so a full `dotnet build` was not possible here. Build locally:

```powershell
cd .\NodalWorkflowLab-Operations
dotnet clean
dotnet build
dotnet run
```

Open `http://localhost:5187/processes?mode=fleet` after stopping any older instance on port 5187.
