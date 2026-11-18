namespace NodalWorkflowLab.Models;

public sealed record ActionCapabilityBinding(string ActionId, string ModuleId, string CapabilityId,
    string SourceRecordId, string CompletionEventType);

/// <summary>Read model composed from the three existing demo stores; no duplicate data ownership.</summary>
public sealed record OperationalView(ProcessSnapshot Processes, OperationsSnapshot Operations,
    ProcessMapSnapshot Map)
{
    public EngineCase? Engine(string id) => Processes.Engines.FirstOrDefault(x => x.Id == id);
    public ProcessFinding? FindingFor(ActionItem action) => Processes.Findings.FirstOrDefault(f =>
        f.Id == action.SourceRecordId || f.ActionId == action.Id || f.InspectionActionId == action.Id);
    public ActionItem? Action(string id) => Operations.Actions.FirstOrDefault(a => a.Id == id);
    public string WorkspaceName(string? id) => Operations.Workspaces.FirstOrDefault(w => w.Id == id)?.Name ?? "Unassigned";
    public string UserName(string? id) => Operations.Users.FirstOrDefault(u => u.Id == id)?.Name ?? "Unassigned";
    public string ActorDisplay(ActionItem a) => string.Join(", ", a.Assignments
        .Where(r => r.Role == "Responsible")
        .Select(r => r.PrincipalKind == "workspace" ? WorkspaceName(r.PrincipalId) : UserName(r.PrincipalId)));
    public string CurrentProcessName(string engineId) => Engine(engineId) is { } e
        ? Map.Get(FleetAnalytics.CurrentProcess(e)).Name : "No repair visit";
    public IEnumerable<ProcessAuditEvent> Trace(string engineId, int count = 12) => Processes.Events
        .Where(e => e.EngineId == engineId).OrderByDescending(e => e.TimestampUtc).Take(count);
    public string WorkbenchUrl(ActionItem action)
    {
        var f = FindingFor(action);
        return f is not null ? "/modules/findings?finding=" + Uri.EscapeDataString(f.Id) :
            "/actions/" + Uri.EscapeDataString(action.Id);
    }
}

public static class OperationalRouting
{
    public static string Atlas(string engineId) => "/processes?mode=fleet&engine=" + Uri.EscapeDataString(engineId);
    public static string Engine(string engineId) => "/processes?mode=execution&engine=" + Uri.EscapeDataString(engineId);
    public static string Action(string actionId) => "/actions/" + Uri.EscapeDataString(actionId);
    public static string Workbench(string findingId) => "/modules/findings?finding=" + Uri.EscapeDataString(findingId);
}
