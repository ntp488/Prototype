using Microsoft.AspNetCore.Hosting;
using System.Text.Json;
using NodalWorkflowLab.Models;

namespace NodalWorkflowLab.Services;

/// <summary>Replace this synthetic demo repository with authenticated application APIs in production.</summary>
public sealed class OperationsStore
{
    public static readonly string[] Statuses = ["Open", "In Progress", "Blocked", "Done"];
    public static readonly string[] Priorities = ["Low", "Normal", "High", "Critical"];
    public static readonly string[] Roles = ["Responsible", "Accountable", "Consulted", "Informed"];

    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _json = new() { WriteIndented = true };
    private OperationsSnapshot _data;

    public OperationsStore(IWebHostEnvironment environment)
    {
        _path = Path.Combine(environment.ContentRootPath, "App_Data", "operations.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _data = File.Exists(_path)
            ? JsonSerializer.Deserialize<OperationsSnapshot>(File.ReadAllText(_path)) ?? throw new InvalidDataException("Invalid operations data.")
            : OperationsSnapshot.Example();
        if (!File.Exists(_path)) File.WriteAllText(_path, JsonSerializer.Serialize(_data, _json));
    }

    /// <summary>Bulk reconcile trace-linked synthetic actions without overwriting user changes.</summary>
    public async Task EnsureFleetActionsAsync(ProcessSnapshot process)
    {
        await _gate.WaitAsync();
        try
        {
            var known = _data.Actions.Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
            int added = 0;
            foreach (var engine in process.Engines.Where(e => e.Id.StartsWith("FLEET-SIM-", StringComparison.Ordinal)))
            {
                var suffix = engine.Id["FLEET-SIM-".Length..];
                var active = engine.Stages.FirstOrDefault(k =>
                    (k.Key == "induct" || k.Key == "teardown" || k.Key == "rebuild" || k.Key == "test") &&
                    (k.Value is "In progress" or "Ready" or "Waiting" or "Blocked"));
                var workspace = active.Key switch
                {
                    "induct" => "induction", "teardown" => "disassembly", "rebuild" or "test" => "assembly",
                    _ => "programs"
                };
                var actionId = "ACT-PROC-" + suffix;
                if (known.Add(actionId))
                {
                    _data.Actions.Add(new ActionItem
                    {
                        Id = actionId, Title = "Coordinate visit milestone · " + engine.WorkOrder,
                        Detail = "Synthetic process coordination; no controlled maintenance authorization.",
                        EngineReference = engine.Id, OwnerWorkspaceId = workspace, AssignedWorkspaceId = workspace,
                        Status = engine.Stages.GetValueOrDefault("release") == "Complete" ? "Done" : "In Progress",
                        Priority = "Normal", CreatedUtc = engine.ReceivedUtc,
                        UpdatedUtc = DateTimeOffset.UtcNow.AddHours(-int.Parse(suffix) % 72),
                        Assignments = [new RaciAssignment { PrincipalKind = "workspace", PrincipalId = workspace, Role = "Responsible" }]
                    });
                    added++;
                }
            }
            foreach (var finding in process.Findings.Where(f => f.Id.StartsWith("FND-FLT-", StringComparison.Ordinal)))
            {
                if (!known.Add(finding.ActionId)) continue;
                // Original finding action represents Engineering work; a separate Quality
                // action owns the subsequent inspection gate.
                var pending = finding.Status is "Pending disposition" or "Disposition revision requested";
                var workspace = "engineering";
                _data.Actions.Add(new ActionItem
                {
                    Id = finding.ActionId, Title = "Review synthetic finding " + finding.Id,
                    Detail = finding.Description, EngineReference = finding.EngineId,
                    OwnerWorkspaceId = workspace, AssignedWorkspaceId = workspace,
                    Priority = pending ? "High" : "Normal",
                    Status = pending ? "Blocked" : "Done",
                    CreatedUtc = DateTimeOffset.UtcNow.AddDays(-5),
                    Assignments = [
                        new RaciAssignment { PrincipalKind = "user", PrincipalId = workspace == "quality" ? "u-sam" : "u-jordan", Role = "Responsible" },
                        new RaciAssignment { PrincipalKind = "workspace", PrincipalId = workspace, Role = "Accountable" }
                    ]
                });
                added++;
            }
            if (added > 0) await WriteUnsafeAsync();
        }
        finally { _gate.Release(); }
    }

    public async Task CompleteFleetStageActionAsync(string engineId)
    {
        await _gate.WaitAsync();
        try
        {
            var id = "ACT-PROC-" + engineId["FLEET-SIM-".Length..];
            var item = _data.Actions.FirstOrDefault(a => a.Id == id);
            if (item is null || item.Status == "Done") return;
            item.Status = "Done";
            item.UpdatedUtc = DateTimeOffset.UtcNow;
            await WriteUnsafeAsync();
        }
        finally { _gate.Release(); }
    }

    public async Task ResetDemoAsync()
    {
        await _gate.WaitAsync();
        try
        {
            _data = OperationsSnapshot.Example();
            await WriteUnsafeAsync();
        }
        finally { _gate.Release(); }
    }

    public async Task<OperationsSnapshot> SnapshotAsync()
    {
        await _gate.WaitAsync();
        try { return _data.Copy(); }
        finally { _gate.Release(); }
    }

    public async Task<ActionItem> CreateActionAsync(string title, string workspaceId, string priority,
        string detail = "", string engineReference = "")
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 180) throw new ArgumentException("Title is required (max 180 characters).");
        if (detail.Length > 2000 || engineReference.Length > 80) throw new ArgumentException("Details too long.");
        if (!Priorities.Contains(priority)) throw new ArgumentException("Unknown priority.");
        await _gate.WaitAsync();
        try
        {
            EnsureWorkspace(workspaceId);
            var item = new ActionItem
            {
                Id = "ACT-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                Title = title.Trim(), Detail = detail, EngineReference = engineReference,
                OwnerWorkspaceId = workspaceId, AssignedWorkspaceId = workspaceId, Priority = priority,
                Assignments = [new() { PrincipalKind = "workspace", PrincipalId = workspaceId, Role = "Responsible" }]
            };
            _data.Actions.Add(item);
            await WriteUnsafeAsync();
            return Clone(item);
        }
        finally { _gate.Release(); }
    }

    public async Task<Workspace> CreateWorkspaceAsync(string name, string parentId)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80) throw new ArgumentException("Workspace name is required (max 80 characters).");
        await _gate.WaitAsync();
        try
        {
            EnsureWorkspace(parentId);
            var workspace = new Workspace
            {
                Id = "ws-" + Guid.NewGuid().ToString("N")[..10],
                ParentId = parentId, Name = name.Trim(), Code = "NEW"
            };
            _data.Workspaces.Add(workspace);
            await WriteUnsafeAsync();
            return Clone(workspace);
        }
        finally { _gate.Release(); }
    }

    /// <summary>Reconcile old seeded action statuses from authoritative synthetic finding state.</summary>
    public async Task<int> ReconcileFindingActionsAsync(ProcessSnapshot process)
    {
        await _gate.WaitAsync();
        try
        {
            var changed=0;
            foreach(var finding in process.Findings)
            {
                var engineering=_data.Actions.FirstOrDefault(a=>a.Id==finding.ActionId);
                if(engineering is not null)
                {
                    var resolved=finding.Status is "Awaiting inspection" or "Cleared (demo)";
                    if(resolved && engineering.Status!="Done") {engineering.Status="Done";engineering.UpdatedUtc=DateTimeOffset.UtcNow;changed++;}
                    // Older fleet seeds used to route the Engineering action itself to Quality.
                    if(finding.Id.StartsWith("FND-FLT-",StringComparison.Ordinal) && engineering.OwnerWorkspaceId!="engineering")
                    {engineering.OwnerWorkspaceId="engineering";engineering.AssignedWorkspaceId="engineering";changed++;}
                }
            }
            if(changed>0)await WriteUnsafeAsync();
            return changed;
        }
        finally{_gate.Release();}
    }

    /// <summary>Idempotent module binding that preserves existing user-assigned RACI.</summary>
    public async Task<int> BindCapabilitiesAsync(IEnumerable<ActionCapabilityBinding> bindings)
    {
        await _gate.WaitAsync();
        try
        {
            var changed = 0;
            foreach (var b in bindings)
            {
                var action = _data.Actions.FirstOrDefault(a => a.Id == b.ActionId);
                if (action is null) continue;
                if (action.ModuleId == b.ModuleId && action.CapabilityId == b.CapabilityId &&
                    action.SourceRecordId == b.SourceRecordId && action.CompletionEventType == b.CompletionEventType) continue;
                action.ModuleId = b.ModuleId;
                action.CapabilityId = b.CapabilityId;
                action.SourceRecordId = b.SourceRecordId;
                action.CompletionEventType = b.CompletionEventType;
                changed++;
            }
            if (changed > 0) await WriteUnsafeAsync();
            return changed;
        }
        finally { _gate.Release(); }
    }

    /// <summary>For module-derived work the UI cannot forge the authoritative completion event.</summary>
    public Task<ActionItem> SetStatusAsync(string id, string status) => SetStatusCoreAsync(id, status, false);

    /// <summary>Called only by the synthetic authoritative module after a validated module operation.</summary>
    internal Task<ActionItem> SetVerifiedStatusAsync(string id, string status) => SetStatusCoreAsync(id, status, true);

    private async Task<ActionItem> SetStatusCoreAsync(string id, string status, bool moduleEventVerified)
    {
        if (!Statuses.Contains(status)) throw new ArgumentException("Unknown status.");
        await _gate.WaitAsync();
        try
        {
            var action = EnsureAction(id);
            if (!moduleEventVerified && status == "Done" && action.RequiresVerifiedCompletion)
                throw new InvalidOperationException("This action requires completion in its source module. Open the workbench to record the underlying operation.");
            action.Status = status;
            action.UpdatedUtc = DateTimeOffset.UtcNow;
            await WriteUnsafeAsync();
            return Clone(action);
        }
        finally { _gate.Release(); }
    }

    public async Task<ActionItem> RouteAsync(string id, string workspaceId)
    {
        await _gate.WaitAsync();
        try
        {
            EnsureWorkspace(workspaceId);
            var action = EnsureAction(id);
            action.AssignedWorkspaceId = workspaceId;
            action.UpdatedUtc = DateTimeOffset.UtcNow;
            await WriteUnsafeAsync();
            return Clone(action);
        }
        finally { _gate.Release(); }
    }

    public async Task<ActionItem> AssignAsync(string id, string principalKind, string principalId, string role)
    {
        if (!Roles.Contains(role)) throw new ArgumentException("Unknown RACI role.");
        if (principalKind is not ("user" or "workspace")) throw new ArgumentException("Unknown principal kind.");
        await _gate.WaitAsync();
        try
        {
            if (principalKind == "workspace") EnsureWorkspace(principalId);
            else if (_data.Users.All(u => u.Id != principalId)) throw new ArgumentException("User not found.");
            var action = EnsureAction(id);
            if (role == "Accountable")
                action.Assignments.RemoveAll(r => r.Role == "Accountable");
            if (!action.Assignments.Any(a => a.PrincipalKind == principalKind && a.PrincipalId == principalId && a.Role == role))
                action.Assignments.Add(new() { PrincipalKind = principalKind, PrincipalId = principalId, Role = role });
            action.UpdatedUtc = DateTimeOffset.UtcNow;
            await WriteUnsafeAsync();
            return Clone(action);
        }
        finally { _gate.Release(); }
    }

    public async Task RemoveAssignmentAsync(string id, string principalKind, string principalId, string role)
    {
        await _gate.WaitAsync();
        try
        {
            var action = EnsureAction(id);
            action.Assignments.RemoveAll(r => r.PrincipalKind == principalKind && r.PrincipalId == principalId && r.Role == role);
            action.UpdatedUtc = DateTimeOffset.UtcNow;
            await WriteUnsafeAsync();
        }
        finally { _gate.Release(); }
    }

    private ActionItem EnsureAction(string id) => _data.Actions.FirstOrDefault(a => a.Id == id)
        ?? throw new ArgumentException("Action item was not found.");
    private void EnsureWorkspace(string id)
    {
        if (!_data.Workspaces.Any(w => w.Id == id)) throw new ArgumentException("Workspace was not found.");
    }
    private static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    private async Task WriteUnsafeAsync()
    {
        var temp = _path + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(_data, _json));
        File.Move(temp, _path, true);
    }
}
