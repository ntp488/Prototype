using NodalWorkflowLab.Models;

namespace NodalWorkflowLab.Services;

/// <summary>
/// Coordinates synthetic modules using their public command surfaces. It does not create
/// an independent shadow copy of engine or work records. Not authentication or regulatory approval.
/// </summary>
public sealed class OperationalCoordinationService
{
    private readonly ProcessOperationsStore _processes;
    private readonly OperationsStore _actions;
    private readonly ProcessMapStore _maps;
    private readonly SemaphoreSlim _initializeGate = new(1, 1);
    private bool _seeded;

    public OperationalCoordinationService(ProcessOperationsStore processes, OperationsStore actions,
        ProcessMapStore maps)
    { _processes = processes; _actions = actions; _maps = maps; }

    private async Task InitializeAsync()
    {
        if (_seeded) return;
        await _initializeGate.WaitAsync();
        try
        {
            if (_seeded) return;
            await _processes.EnsureDemoFleetAsync();
            await _processes.EnsurePendingInspectionActionsAsync();
            _seeded = true;
        }
        finally { _initializeGate.Release(); }
    }

    public async Task<OperationalView> ReadAsync()
    {
        await InitializeAsync();
        var processes = await _processes.SnapshotAsync();
        await _actions.ReconcileFindingActionsAsync(processes);
        // Bind all source-backed actions, including newly created downstream inspection tasks.
        var bindings = new List<ActionCapabilityBinding>();
        foreach (var finding in processes.Findings)
        {
            bindings.Add(new(finding.ActionId, "findings", "engineering.disposition", finding.Id,
                "engineering.disposition.recorded"));
            if (!string.IsNullOrWhiteSpace(finding.InspectionActionId))
                bindings.Add(new(finding.InspectionActionId, "quality", "inspection.record", finding.Id,
                    "quality.inspection.recorded"));
        }
        foreach (var engine in processes.Engines.Where(e => !string.IsNullOrWhiteSpace(e.IntakeActionId)))
            bindings.Add(new(engine.IntakeActionId!, "induction", "intake.complete", engine.Id,
                "intake.gate1.completed"));
        await _actions.BindCapabilitiesAsync(bindings);
        return new OperationalView(processes, await _actions.SnapshotAsync(), await _maps.SnapshotAsync());
    }

    public Task<string> SubmitDispositionAsync(string findingId, bool accept) =>
        _processes.RecordDispositionAsync(findingId, accept);
    public Task<string> ResubmitAsync(string findingId) => _processes.ResubmitDispositionAsync(findingId);
    public Task<string> RecordInspectionAsync(string findingId, bool accept) =>
        _processes.RecordInspectionAsync(findingId, accept);
    public Task<string> ReportFindingAsync(string engineId, string partId, string description) =>
        _processes.ReportFindingAsync(engineId, partId, description);
    public Task<string> MovePartAsync(string engineId, string partId, string destination) =>
        _processes.MovePartAsync(engineId, partId, destination);
    public Task<string> AdvanceIntakeAsync(string engineId) => _processes.AdvanceIntakeAsync(engineId);
    public Task<string> ReceiveEngineAsync(string family, string aircraft, string workOrder) =>
        _processes.ReceiveEngineAsync(family, aircraft, workOrder);

    public async Task<ActionItem> CreateGeneralActionAsync(string title, string workspaceId, string priority,
        string engineId, string? assignedUserId)
    {
        var action=await _actions.CreateActionAsync(title,workspaceId,priority,
            "User-created synthetic coordination action.",engineId);
        if (!string.IsNullOrEmpty(assignedUserId) && assignedUserId != "all")
            await _actions.AssignAsync(action.Id,"user",assignedUserId,"Responsible");
        await _processes.RecordGeneralActionEventAsync(engineId,action.Id,title);
        return action;
    }

}
