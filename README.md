# Prototyping — Nodal Connected Operations

> ### Start here if you are implementing Nodal in another application
>
> **Nodal's intended outcome is an application-wide UX and interaction transformation, not merely workflow/backend integration.** Preserve correct domain logic, but do not preserve legacy navigation, dashboards, or disconnected module page layouts by default.
>
> - **[Product Intent / Experience North Star](docs/PRODUCT_INTENT.md)** — the vision, user journeys and non-negotiable experience requirements.
> - **[Implementation Architecture / Migration Blueprint](docs/IMPLEMENTATION_ARCHITECTURE.md)** — how to preserve authoritative domain capabilities while replacing the presentation layer.
> - **[UX Acceptance / Agent Delivery Criteria](docs/UX_ACCEPTANCE_AND_DELIVERY.md)** — concrete, testable definition of done.
> - **[Coding Agent Instructions](AGENTS.md)** — read this before making implementation changes.
>
> The flagship interaction is **process graph → full-height filtered engine table → animated engine spotlight → typed action → domain workbench → correlated workflow/dashboards**, preserving context in both directions.
>
---

An experimental .NET 10 Blazor Interactive Server application for modeling manufacturing process graphs, synthetic engine-repair visits, operational workflows, fleet analysis, workspace responsibilities, and action-item orchestration.

**Status: prototype / demonstration only.** The provided engine visits, parts, findings, approvals, and operational statistics are fictitious. This software is not a maintenance-record system, not validated for regulated use, and not affiliated with or endorsed by Pratt & Whitney or RTX.

## Run

Prerequisite: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
cd NodalWorkflowLab-Operations
dotnet restore
dotnet build
dotnet run
```

Open `http://localhost:5187/processes?mode=fleet` for Fleet Atlas, or `http://localhost:5187/work` for My Work.

## Smoke tests

```powershell
dotnet run --project ./NodalWorkflowLab-Operations-SmokeTests/NodalWorkflowLab.SmokeTests.csproj
```

## Repository contents

- `NodalWorkflowLab-Operations/` — Blazor project, UI, data models, services, and synthetic data.
- `NodalWorkflowLab-Operations-SmokeTests/` — regression/smoke-test console project.
- `START-HERE.md`, `IMPLEMENTATION-NOTES.md`, and other `.md` files — prototyping notes.

## Scope and safety

Use synthetic data only. Do not add controlled technical data, real engine maintenance information, employee records, credentials, or proprietary company materials. Before any real-world deployment, replace demonstration authorization, persistence, and approvals with validated enterprise services and applicable regulatory controls.

No open-source license has been selected for this repository.
