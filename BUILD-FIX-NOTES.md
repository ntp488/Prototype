# Build correction — Razor variable shadowing

This replacement addresses the three `CS0136` errors reported during `dotnet build`:

- `Components/Pages/PartsWorkbench.razor`: rename the list iteration variable to `partItem`; preserve the selected detail variable `part`.
- `Components/Pages/IntakeWorkbench.razor`: rename the list iteration variable to `engineItem`; preserve the selected detail variable `e`.
- `Components/Pages/FindingsWorkbench.razor`: rename the list iteration variable to `findingItem`; preserve the selected detail variable `finding`.

All other existing project files are unchanged.

## Run

Extract into a new folder, then from `NodalWorkflowLab-Operations`:

```powershell
dotnet clean
dotnet restore
dotnet build
dotnet run
```

You can also apply the same three renames directly to your existing checkout.

**Validation:** Compared contents against the original ZIP; only the three Razor files listed above differ. Verified the corrected identifier references and ZIP CRC/integrity. The .NET SDK is not installed in the build environment, so a full compile could not be run here.
