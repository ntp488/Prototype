# Nodal Operations — Connected Work Center release

## Prerequisites
- .NET 10 SDK
- Windows PowerShell, terminal, or Visual Studio 2026 with ASP.NET workload

## Run
1. Extract the ZIP into a **new directory**. Existing extracted versions have separate `App_Data` folders.
2. From the extracted root:

```powershell
cd .\NodalWorkflowLab-Operations
dotnet restore
dotnet build
dotnet run
```

3. Navigate to:

| Path | What it does |
|---|---|
| `http://localhost:5187/work` | Personalized operational Work Center (choose a synthetic viewer) |
| `/workspaces` | Workspace hierarchy, automatic action rollups, process impact, filtered work |
| `/actions/ACT-1002` | Typed action detail, RACI assignment, module completion contract |
| `/modules` | Site-module directory |
| `/modules/findings?finding=FND-SIM-001` | Engineering disposition, Quality review, waiting-workflow demonstration |
| `/modules/parts` | Part-ID lookup, hold-aware movement and recorded rejected scans |
| `/modules/intake` | Generic Gate 1 workbench |
| `/processes?mode=fleet&engine=PW-DEMO-1007` | Fleet Atlas with synthetic engine spotlight |
| `/processes?mode=design` | Site-wide process definition editor |
| `/` | Original Blender-style node workflow designer (preserved) |

If port 5187 is occupied, stop the older Blazor process with Ctrl+C before starting this build. `dotnet run --no-launch-profile --urls http://127.0.0.1:5190` uses a separate port.

## Demonstration: operational loop
1. Open `/work`, leave **Jordan Lee — Repair Engineering** selected.
2. Select **ACT-1002** or another **Pending disposition** finding. Open its source workbench.
3. In `/modules/findings`, choose the **Engineering reviewer** simulated role and select **Accept for inspection**.
4. Observe that a **separate Quality task** appears and the part remains held.
5. Switch to **Quality inspector** in the workbench. Record a simulated acceptance.
6. The synthetic Quality action completes, the persisted workflow resumes, and part/engine states update. Open the linked action or **Locate in Fleet Atlas**.
7. Switch to **Sam Patel — Quality** in `/work`; inspect the Quality queue and workspace rollup.

**No simulated decision is an FAA sign-off or aircraft maintenance release.** Role switching is a *demo preview*, not identity/authentication or qualification verification.

## Automated smoke checks
From the ZIP's extracted root:

```powershell
dotnet run --project .\NodalWorkflowLab-Operations-SmokeTests\NodalWorkflowLab.SmokeTests.csproj
```

Smoke tests check original process/fleet behaviors plus source-backed completion guards, downstream inspection task generation, RACI bindings, and cross-module deep links.

## Persistence and privacy
Data lives under `NodalWorkflowLab-Operations/App_Data/*.json`. The ZIP contains no company network access, controlled maintenance information, real personnel data or credentials. This is a **single-process synthetic demo**. The existing JSON writes are not multi-service transactions, and the app has no enterprise SSO, audited electronic-signature controls, or real module APIs. Do not use it for actual engine maintenance decisions.

## Validation limitations
The source and CSS/JavaScript can be inspected in the included package. The authoring environment did not have the .NET 10 SDK, so **a successful local `dotnet build` and smoke test execution remain required**. The provided preview PNGs were rendered from static HTML fixtures using the same stylesheet (not the running Blazor server).
