# Release notes — Connected Work Center & Site Modules

**Base:** Nodal Fleet Intelligence Atlas (previous Math.Round correction included)

### Added
- Atlas-themed global navigation / responsive design system
- New Work Center, Workspace Intelligence, Action Detail and Site Modules pages
- Shared Operational Context Lens (engine, dependencies, source action, events and navigation)
- Typed action metadata and module-backed `Done` safeguard
- Engineering → Quality action generation through existing source module simulation
- Individual demo RACI assignments for seeded fleet findings and new module-created actions
- Legacy demo inspection-task migration for already-waiting findings
- Fleet Atlas engine spotlight → action deep links and module links from Engine Execution
- Browser-rendered static UX previews and expanded dependency-free C# smoke tests

### Preserved
- Multiple workflow editor / process maps / nested process hierarchy
- Universal Gate 1 intake
- 219 initial synthetic repair visits (plus user-created visits), part/location/hold/findings data
- One-second randomized fleet progress simulation, graph motion, movable nodes, full-screen graph and table navigation, replay/heatmaps/forecast/investigations

### Not included
- Actual Pratt & Whitney integration, enterprise user authentication, regulatory e-signatures, production data lineage guarantees, or server-scale durable orchestration. Domain module demonstration only.

### Build status
- Source, route/configuration, CSS and JavaScript static checks passed.
- Playwright + Chromium visual previews rendered for Work Center and Findings workbench.
- **Full .NET compilation and C# smoke tests could not run in the authoring environment (SDK unavailable).** Run them locally before using the build.
