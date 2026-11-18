using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using NodalWorkflowLab.Models;
using NodalWorkflowLab.Services;

namespace NodalWorkflowLab.Components.Pages;

public partial class Home
{
    [SupplyParameterFromQuery(Name = "workflow")] public string? RequestedWorkflowId { get; set; }
    [SupplyParameterFromQuery(Name = "view")] public string? RequestedView { get; set; }
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private WorkflowRunner Runner { get; set; } = default!;
    [Inject] private WorkflowLibraryStore Library { get; set; } = default!;
    [Inject] private OperationsStore Operations { get; set; } = default!;

    private static readonly string[] Categories = ["Triggers", "Site Modules", "Composition", "Operations", "Data", "Logic", "Outputs"];
    private static readonly JsonSerializerOptions ExportOptions = new() { WriteIndented = true };
    private List<WorkflowDocument> Workflows = [];
    private WorkflowDocument Current = new();
    private WorkflowGraph Graph => Current.Graph;
    private OperationsSnapshot Data = new();
    private string View = "editor";
    private string WorkspaceId = "cec";
    private string? ActionId;
    private string? SelectedNodeId;
    private string? SelectedEdgeId;
    private string? ActiveEdgeId;
    private readonly Dictionary<string, string> NodeStatuses = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> LastPayload = new(StringComparer.Ordinal);
    private readonly List<ConsoleLogLine> EventLog = [];

    private string Search = "";
    private string TestPayload = "Test event";
    private string TestActionId = "ACT-1002";
    private string NewActionTitle = "";
    private string NewActionPriority = "Normal";
    private string NewEngineReference = "PW-DEMO-1007";
    private string NewWorkspaceName = "";
    private string RaciKind = "user";
    private string RaciPrincipalId = "u-alex";
    private string RaciRole = "Responsible";
    private bool IsRunning;
    private bool ConsoleCollapsed;
    private bool SidebarOpen;
    private bool InspectorOpen;
    private bool _initialized;
    private bool _fitRequested;
    private int _eventSequence;
    private DotNetObjectReference<Home>? _interopRef;
    private CancellationTokenSource? _runCancellation;

    private WorkflowNode? SelectedNode => Graph.Nodes.FirstOrDefault(n => n.Id == SelectedNodeId);
    private WorkflowEdge? SelectedEdge => Graph.Edges.FirstOrDefault(e => e.Id == SelectedEdgeId);
    private ActionItem? SelectedAction => Data.Actions.FirstOrDefault(a => a.Id == ActionId);
    private Workspace? SelectedWorkspace => Data.Workspaces.FirstOrDefault(w => w.Id == WorkspaceId);
    private WorkspaceSummary? CurrentSummary => SelectedWorkspace is null ? null : OperationsRollups.Summarize(Data, WorkspaceId);
    private List<ActionItem> VisibleActions => SelectedWorkspace is null ? [] :
        OperationsRollups.ActionsFor(Data, WorkspaceId).OrderByDescending(a => a.UpdatedUtc).ToList();
    private string StatusOf(string id) => NodeStatuses.GetValueOrDefault(id, "");
    private static string Trim(string value, int max) => value.Length > max ? value[..(max - 1)] + "…" : value;
    private static string LogColor(string kind) => kind switch
    {
        "node-complete" or "run-complete" => "success",
        "run-error" or "run-canceled" => "error",
        "message" or "automation" => "warning",
        _ => ""
    };
    private string WorkspaceName(string? id) => Data.Workspaces.FirstOrDefault(w => w.Id == id)?.Name ?? "Unassigned";
    private string UserName(string id) => Data.Users.FirstOrDefault(u => u.Id == id)?.Name ?? id;
    private string PrincipalName(RaciAssignment r) => r.PrincipalKind == "workspace" ? WorkspaceName(r.PrincipalId) : UserName(r.PrincipalId);
    private int LevelOf(Workspace w)
    {
        var depth = 0;
        var parent = w.ParentId;
        while (parent is not null && depth < 20)
        {
            depth++;
            parent = Data.Workspaces.FirstOrDefault(x => x.Id == parent)?.ParentId;
        }
        return depth;
    }
    private IEnumerable<Workspace> OrderedWorkspaces()
    {
        var result = new List<Workspace>();
        void Walk(string? parent)
        {
            foreach (var w in Data.Workspaces.Where(x => x.ParentId == parent).OrderBy(x => x.Name))
            {
                result.Add(w);
                Walk(w.Id);
            }
        }
        Walk(null);
        return result;
    }

    protected override async Task OnInitializedAsync()
    {
        await RefreshAllAsync();
        Current = Workflows.FirstOrDefault(w => w.Id == RequestedWorkflowId)
            ?? Workflows.FirstOrDefault(w => w.Id == "blocked-escalation") ?? Workflows.First();
        if (RequestedView is "editor" or "operations" or "dashboards") View = RequestedView;
        ActionId = Data.Actions.FirstOrDefault()?.Id;
        if (!Data.Actions.Any(a => a.Id == TestActionId)) TestActionId = Data.Actions.FirstOrDefault()?.Id ?? "";
    }

    private async Task RefreshAllAsync()
    {
        Workflows = await Library.ListAsync();
        Data = await Operations.SnapshotAsync();
    }
    private async Task RefreshDataAsync() => Data = await Operations.SnapshotAsync();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _interopRef = DotNetObjectReference.Create(this);
            await JS.InvokeAsync<string?>("nodalCanvas.initialize", "viewport", _interopRef);
            _initialized = true;
            _fitRequested = true;
            await InvokeAsync(StateHasChanged);
            return;
        }
        if (!_initialized) return;
        await JS.InvokeVoidAsync("nodalCanvas.renderEdges", "viewport", Graph.Edges, SelectedEdgeId, ActiveEdgeId);
        if (_fitRequested && View == "editor")
        {
            _fitRequested = false;
            await JS.InvokeVoidAsync("nodalCanvas.fit", "viewport");
        }
    }

    private async Task NotifyAsync(string message)
    {
        if (_initialized) await JS.InvokeVoidAsync("nodalCanvas.toast", "viewport", message);
    }
    private async Task SaveCurrentAsync(bool showToast = false)
    {
        await Library.SaveAsync(Current);
        Workflows = await Library.ListAsync();
        if (showToast) await NotifyAsync("Workflow saved on server");
    }
    private Task PersistAsync() => SaveCurrentAsync();

    private async Task ChangeViewAsync(string view)
    {
        View = view;
        SidebarOpen = false;
        InspectorOpen = false;
        if (view == "editor") _fitRequested = true;
        await RefreshDataAsync();
    }
    private void ToggleSidebar() { SidebarOpen = !SidebarOpen; InspectorOpen = false; }
    private void ToggleInspector() { InspectorOpen = !InspectorOpen; SidebarOpen = false; }
    private void ToggleConsole() => ConsoleCollapsed = !ConsoleCollapsed;
    private void ClearLog() => EventLog.Clear();
    private Task ZoomAsync(double factor) => JS.InvokeVoidAsync("nodalCanvas.zoomBy", "viewport", factor).AsTask();
    private Task FitAsync() => JS.InvokeVoidAsync("nodalCanvas.fit", "viewport").AsTask();
    private Task OpenImportAsync() => JS.InvokeVoidAsync("nodalCanvas.openImport", "file-input").AsTask();
    private Task ExportAsync() => JS.InvokeVoidAsync("nodalCanvas.download", $"workflow-{Current.Id}.json",
        JsonSerializer.Serialize(Current, ExportOptions)).AsTask();

    private async Task SelectWorkflowAsync(string id)
    {
        if (IsRunning || id == Current.Id) return;
        var doc = await Library.GetAsync(id);
        if (doc is null) return;
        Current = doc;
        ResetCanvasState();
        View = "editor";
        await JS.InvokeVoidAsync("nodalCanvas.cancelConnection", "viewport");
    }
    private void ResetCanvasState()
    {
        SelectedNodeId = null; SelectedEdgeId = null; ActiveEdgeId = null;
        NodeStatuses.Clear(); LastPayload.Clear(); _fitRequested = true;
    }
    private async Task NewWorkflowAsync()
    {
        if (IsRunning) return;
        var next = new WorkflowDocument { Name = "New workflow", Enabled = false,
            Description = "Design and test a new process automation.",
            Graph = new WorkflowGraph { Nodes = [WorkflowNode.Create("start-" + Guid.NewGuid().ToString("N")[..8], "trigger", 90, 200, ("message", "Hello workflow"))] } };
        await Library.SaveAsync(next);
        Workflows = await Library.ListAsync(); Current = next;
        ResetCanvasState(); View = "editor";
        await NotifyAsync("Workflow created and saved");
    }
    private async Task DuplicateWorkflowAsync()
    {
        if (IsRunning) return;
        var clone = Current.Copy();
        clone.Id = Guid.NewGuid().ToString("N");
        clone.Name = Trim(Current.Name + " (copy)", 100);
        clone.Enabled = false;
        await Library.SaveAsync(clone);
        Workflows = await Library.ListAsync(); Current = clone;
        ResetCanvasState();
        await NotifyAsync("Workflow duplicated (disabled by default)");
    }
    private async Task DeleteWorkflowAsync()
    {
        if (IsRunning) return;
        try
        {
            await Library.DeleteAsync(Current.Id);
            Workflows = await Library.ListAsync(); Current = Workflows.First();
            ResetCanvasState(); await NotifyAsync("Workflow deleted");
        }
        catch (InvalidOperationException ex) { await NotifyAsync(ex.Message); }
    }
    private async Task RenameWorkflowAsync(ChangeEventArgs e)
    {
        var name = e.Value?.ToString()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(name)) { await NotifyAsync("Workflow name is required"); return; }
        Current.Name = name.Length > 100 ? name[..100] : name;
        await SaveCurrentAsync();
    }
    private async Task UpdateWorkflowDescriptionAsync(ChangeEventArgs e)
    {
        var description = e.Value?.ToString() ?? "";
        Current.Description = description.Length > 1000 ? description[..1000] : description;
        await SaveCurrentAsync();
    }
    private async Task SetWorkflowEnabledAsync(ChangeEventArgs e)
    {
        Current.Enabled = e.Value is bool b && b;
        await SaveCurrentAsync();
    }
    private static string NewId(string type) => $"{type}-{Guid.NewGuid():N}"[..(type.Length + 9)];

    private static WorkflowNode CreateDefaultNode(string type, GraphPosition p) => type switch
    {
        "trigger" => WorkflowNode.Create(NewId(type), type, p.X, p.Y, ("message", "Manual event")),
        "action-event" => WorkflowNode.Create(NewId(type), type, p.X, p.Y, ("eventType", "status-changed")),
        "load-action" => WorkflowNode.Create(NewId(type), type, p.X, p.Y),
        "action-status" or "set-status" => WorkflowNode.Create(NewId(type), type, p.X, p.Y, ("status", "Blocked")),
        "route-workspace" => WorkflowNode.Create(NewId(type), type, p.X, p.Y, ("workspaceId", "engineering")),
        "assign-raci" => WorkflowNode.Create(NewId(type), type, p.X, p.Y, ("principalKind", "user"), ("principalId", "u-jordan"), ("role", "Responsible")),
        "create-action" => WorkflowNode.Create(NewId(type), type, p.X, p.Y, ("title", "Follow-up for {{actionTitle}}"), ("workspaceId", "quality"), ("priority", "Normal")),
        "workspace-rollup" => WorkflowNode.Create(NewId(type), type, p.X, p.Y, ("workspaceId", "cec")),
        "notify" => WorkflowNode.Create(NewId(type), type, p.X, p.Y, ("message", "Notice: {{actionTitle}}")),
        "text" => WorkflowNode.Create(NewId(type), type, p.X, p.Y, ("template", "Received: {{input}}")),
        "transform" => WorkflowNode.Create(NewId(type), type, p.X, p.Y, ("operation", "uppercase")),
        "condition" => WorkflowNode.Create(NewId(type), type, p.X, p.Y, ("operator", "includes"), ("value", "HELLO")),
        "delay" => WorkflowNode.Create(NewId(type), type, p.X, p.Y, ("ms", "450")),
        "log" or "finish" => WorkflowNode.Create(NewId(type), type, p.X, p.Y, ("label", type == "log" ? "Console Log" : "Workflow complete")),
        _ => throw new ArgumentException("Unknown node type")
    };
    private async Task AddNodeAsync(string type)
    {
        if (IsRunning || !NodeCatalog.Types.ContainsKey(type)) return;
        var pos = await JS.InvokeAsync<GraphPosition>("nodalCanvas.centerWorld", "viewport");
        var node = CreateDefaultNode(type, pos);
        node.X = Math.Round(node.X - 119); node.Y = Math.Round(node.Y - 95);
        Graph.Nodes.Add(node);
        SelectedNodeId = node.Id; SelectedEdgeId = null; SidebarOpen = false;
        await PersistAsync(); await NotifyAsync($"Added {NodeCatalog.Types[type].Title}");
    }
    private async Task UpdateConfigAsync(string nodeId, string key, string value)
    {
        if (IsRunning) return;
        var node = Graph.Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null) return;
        node.SetConfig(key, value);
        await PersistAsync();
    }
    private async Task DuplicateNodeAsync(string nodeId)
    {
        if (IsRunning) return;
        var existing = Graph.Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (existing is null) return;
        var clone = WorkflowNode.Create(NewId(existing.Type), existing.Type, existing.X + 44, existing.Y + 52);
        foreach (var field in existing.Config) clone.Config[field.Key] = field.Value.Clone();
        Graph.Nodes.Add(clone); SelectedNodeId = clone.Id; SelectedEdgeId = null;
        await PersistAsync(); await NotifyAsync("Node duplicated");
    }
    private async Task DeleteSelectedAsync()
    {
        if (IsRunning) return;
        if (SelectedNodeId is not null)
        {
            Graph.Nodes.RemoveAll(n => n.Id == SelectedNodeId);
            Graph.Edges.RemoveAll(e => e.From == SelectedNodeId || e.To == SelectedNodeId);
            SelectedNodeId = null;
        }
        else if (SelectedEdgeId is not null)
        {
            Graph.Edges.RemoveAll(e => e.Id == SelectedEdgeId); SelectedEdgeId = null;
        }
        else return;
        await JS.InvokeVoidAsync("nodalCanvas.cancelConnection", "viewport");
        await PersistAsync(); await NotifyAsync("Selection deleted");
    }
    private async Task ResetAsync()
    {
        if (IsRunning) return;
        Current.Graph = WorkflowGraph.Example();
        ResetCanvasState();
        await JS.InvokeVoidAsync("nodalCanvas.cancelConnection", "viewport");
        await PersistAsync(); await NotifyAsync("Graph replaced with example");
    }
    private async Task ImportAsync(InputFileChangeEventArgs args)
    {
        if (IsRunning) return;
        try
        {
            await using var content = args.File.OpenReadStream(2 * 1024 * 1024);
            using var reader = new StreamReader(content);
            var json = await reader.ReadToEndAsync();
            using var parsed = JsonDocument.Parse(json);
            WorkflowDocument incoming;
            if (parsed.RootElement.TryGetProperty("Graph", out _))
            {
                incoming = JsonSerializer.Deserialize<WorkflowDocument>(json) ?? throw new InvalidDataException("Invalid workflow JSON");
                incoming.Id = Guid.NewGuid().ToString("N"); // importing never overwrites an existing workflow
                incoming.Enabled = false;
                incoming.Name = Trim(incoming.Name + " (imported)", 100);
            }
            else
            {
                var graph = JsonSerializer.Deserialize<WorkflowGraph>(json) ?? throw new InvalidDataException("Invalid graph JSON");
                incoming = new() { Name = "Imported graph", Enabled = false, Graph = graph };
            }
            if (incoming.Graph is null) throw new InvalidDataException("Imported workflow has no graph.");
            if (!incoming.Graph.Validate(out var problem)) throw new InvalidDataException(problem);
            await Library.SaveAsync(incoming);
            Workflows = await Library.ListAsync(); Current = incoming;
            ResetCanvasState(); View = "editor";
            await JS.InvokeVoidAsync("nodalCanvas.cancelConnection", "viewport");
            await NotifyAsync("Imported as a new, disabled workflow");
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException or ArgumentException)
        { await NotifyAsync("Import failed: " + ex.Message); }
    }

    private Task RunDefaultAsync()
    {
        var start = Graph.Nodes.FirstOrDefault(n => n.Type is "trigger" or "action-event") ?? Graph.Nodes.FirstOrDefault();
        return start is null ? NotifyAsync("Add a node first") : RunSelectedAsync(start.Id);
    }
    private Task RunSelectedAsync(string startId, string? payload = null)
    {
        var start = Graph.Nodes.FirstOrDefault(n => n.Id == startId);
        var eventType = start?.Type == "action-event" ? start.ConfigText("eventType", "status-changed") : null;
        if (eventType == "any") eventType = "status-changed";
        var input = new WorkflowInput(payload ?? (start?.Type == "trigger" ? null : TestPayload),
            string.IsNullOrEmpty(TestActionId) ? null : TestActionId, eventType);
        return RunWorkflowAsync(Current, startId, input);
    }
    private async Task RunWorkflowAsync(WorkflowDocument workflow, string startId, WorkflowInput input)
    {
        if (IsRunning || !workflow.Graph.Nodes.Any(n => n.Id == startId)) return;
        IsRunning = true; NodeStatuses.Clear(); LastPayload.Clear(); ActiveEdgeId = null;
        _runCancellation = new CancellationTokenSource();
        await InvokeAsync(StateHasChanged);
        try
        {
            await Runner.RunAsync(workflow.Graph.Copy(), startId, input,
                evt => PublishAsync(evt, workflow.Id, workflow.Name), _runCancellation.Token);
        }
        catch (OperationCanceledException)
        { await PublishAsync(new("run-canceled", Text: "Execution canceled."), workflow.Id, workflow.Name); }
        catch (Exception ex)
        {
            await PublishAsync(new("run-error", Text: ex.Message), workflow.Id, workflow.Name);
            await NotifyAsync("Execution stopped: " + ex.Message);
        }
        finally
        {
            IsRunning = false; ActiveEdgeId = null;
            _runCancellation.Dispose(); _runCancellation = null;
            await RefreshDataAsync();
            await InvokeAsync(StateHasChanged);
        }
    }
    private void CancelRun() => _runCancellation?.Cancel();

    private async Task PublishAsync(WorkflowEvent evt, string workflowId, string workflowName)
    {
        if (Current.Id == workflowId)
        {
            if (evt.Type == "node-start" && evt.NodeId is not null) NodeStatuses[evt.NodeId] = "running";
            if (evt.Type == "node-complete" && evt.NodeId is not null)
            {
                NodeStatuses[evt.NodeId] = "succeeded";
                LastPayload[evt.NodeId] = evt.Payload ?? "";
            }
            if (evt.Type == "edge-traverse") ActiveEdgeId = evt.EdgeId;
            if (evt.Type == "edge-idle") ActiveEdgeId = null;
        }
        if (evt.Type != "edge-idle")
        {
            var node = Current.Id == workflowId ? Graph.Nodes.FirstOrDefault(n => n.Id == evt.NodeId) : null;
            var description = evt.Text ?? string.Join(" ", new[]
            {
                node is null ? evt.NodeId ?? "" : NodeCatalog.Types[node.Type].Title,
                evt.Port is null ? "" : $"[{evt.Port}]",
                evt.Payload is null ? "" : "→ " + Trim(evt.Payload, 150)
            }.Where(s => s.Length > 0));
            AddLog(evt.Type, workflowName + " · " + description);
        }
        await InvokeAsync(StateHasChanged);
        if (_initialized && evt.Type != "edge-idle")
        {
            await JS.InvokeVoidAsync("nodalCanvas.publish", evt.Type, new
            {
                workflowId, nodeId = evt.NodeId, payload = evt.Payload,
                port = evt.Port, text = evt.Text, edgeId = evt.EdgeId,
                at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            await JS.InvokeVoidAsync("nodalCanvas.scrollLog", "console-stream");
        }
    }
    private void AddLog(string type, string text)
    {
        EventLog.Add(new(++_eventSequence, DateTimeOffset.Now.ToString("HH:mm:ss"), type, text));
        if (EventLog.Count > 350) EventLog.RemoveAt(0);
    }

    // Event ingress for the prototype: UI actions are persisted first and dispatch to enabled workflows.
    // Workflow-generated writes deliberately do NOT re-dispatch (prevents unbounded feedback loops).
    private async Task DispatchActionEventAsync(ActionEvent evt)
    {
        AddLog("automation", $"Action {evt.Kind}: {evt.ActionId} · scanning enabled workflows");
        var workflows = await Library.ListAsync();
        foreach (var workflow in workflows.Where(w => w.Enabled))
        {
            foreach (var start in workflow.Graph.Nodes.Where(n => n.Type == "action-event"))
            {
                var wanted = start.ConfigText("eventType", "status-changed");
                if (wanted != "any" && wanted != evt.Kind) continue;
                var scoped = start.ConfigText("workspaceId");
                if (!string.IsNullOrEmpty(scoped))
                {
                    var ids = OperationsRollups.DescendantIds(Data, scoped);
                    var item = Data.Actions.FirstOrDefault(a => a.Id == evt.ActionId);
                    if (item is null || (!ids.Contains(item.OwnerWorkspaceId) &&
                        (item.AssignedWorkspaceId is null || !ids.Contains(item.AssignedWorkspaceId)))) continue;
                }
                await RunWorkflowAsync(workflow, start.Id,
                    new WorkflowInput(evt.Message, evt.ActionId, evt.Kind));
            }
        }
        await RefreshDataAsync();
    }

    private async Task CreateActionAsync()
    {
        if (IsRunning) return;
        try
        {
            var created = await Operations.CreateActionAsync(NewActionTitle, WorkspaceId, NewActionPriority,
                engineReference: NewEngineReference);
            NewActionTitle = ""; ActionId = created.Id; TestActionId = created.Id;
            await RefreshDataAsync();
            AddLog("message", $"Created {created.Id} in {WorkspaceName(created.OwnerWorkspaceId)}");
            await DispatchActionEventAsync(new("created", created.Id, created.OwnerWorkspaceId, created.Title));
        }
        catch (ArgumentException ex) { await NotifyAsync(ex.Message); }
    }
    private async Task ChangeActionStatusAsync(string actionId, string status)
    {
        if (IsRunning) return;
        try
        {
            var current = Data.Actions.FirstOrDefault(a => a.Id == actionId);
            if (current is null || current.Status == status) return;
            await Operations.SetStatusAsync(actionId, status);
            await RefreshDataAsync();
            await DispatchActionEventAsync(new("status-changed", actionId, current.OwnerWorkspaceId, status));
        }
        catch (Exception ex) { await NotifyAsync(ex.Message); }
    }
    private async Task RouteActionAsync(string actionId, string workspaceId)
    {
        if (IsRunning) return;
        try
        {
            var old = Data.Actions.FirstOrDefault(a => a.Id == actionId);
            if (old is null || old.AssignedWorkspaceId == workspaceId) return;
            await Operations.RouteAsync(actionId, workspaceId);
            await RefreshDataAsync();
            await DispatchActionEventAsync(new("assigned", actionId, workspaceId, workspaceId));
        }
        catch (ArgumentException ex) { await NotifyAsync(ex.Message); }
    }
    private async Task ResetDemoDataAsync()
    {
        if (IsRunning) return;
        var confirmed = await JS.InvokeAsync<bool>("confirm", "Reset all sample workspaces, action items and RACI assignments? This cannot be undone.");
        if (!confirmed) return;
        await Operations.ResetDemoAsync();
        WorkspaceId = "cec";
        ActionId = "ACT-1002";
        TestActionId = "ACT-1002";
        await RefreshDataAsync();
        AddLog("message", "Reset synthetic operations data to seed state");
        await NotifyAsync("Demo operations data restored");
    }

    private async Task CreateWorkspaceAsync()
    {
        if (IsRunning) return;
        try
        {
            var w = await Operations.CreateWorkspaceAsync(NewWorkspaceName, WorkspaceId);
            NewWorkspaceName = ""; WorkspaceId = w.Id;
            await RefreshDataAsync(); await NotifyAsync("Child workspace created");
        }
        catch (ArgumentException ex) { await NotifyAsync(ex.Message); }
    }
    private void ChangeRaciKind(ChangeEventArgs e)
    {
        RaciKind = e.Value?.ToString() ?? "user";
        RaciPrincipalId = RaciKind == "user" ? Data.Users.FirstOrDefault()?.Id ?? "" : Data.Workspaces.FirstOrDefault()?.Id ?? "";
    }
    private async Task AddRaciAsync()
    {
        if (IsRunning || SelectedAction is null) return;
        try
        {
            var actionId = SelectedAction.Id;
            await Operations.AssignAsync(actionId, RaciKind, RaciPrincipalId, RaciRole);
            await RefreshDataAsync();
            await DispatchActionEventAsync(new("assigned", actionId, SelectedAction?.AssignedWorkspaceId,
                $"RACI {RaciRole} → {RaciPrincipalId}"));
            await NotifyAsync("RACI assignment saved");
        }
        catch (ArgumentException ex) { await NotifyAsync(ex.Message); }
    }
    private async Task RemoveRaciAsync(RaciAssignment assignment)
    {
        if (IsRunning || SelectedAction is null) return;
        var actionId = SelectedAction.Id;
        await Operations.RemoveAssignmentAsync(actionId, assignment.PrincipalKind, assignment.PrincipalId, assignment.Role);
        await RefreshDataAsync();
        await DispatchActionEventAsync(new("assigned", actionId, SelectedAction?.AssignedWorkspaceId,
            $"Removed {assignment.Role} assignment"));
    }

    [JSInvokable] public async Task OnNodeSelected(string id)
    {
        SelectedNodeId = Graph.Nodes.Any(n => n.Id == id) ? id : null;
        SelectedEdgeId = null; await InvokeAsync(StateHasChanged);
    }
    [JSInvokable] public async Task OnEdgeSelected(string id)
    {
        SelectedEdgeId = Graph.Edges.Any(e => e.Id == id) ? id : null;
        SelectedNodeId = null; await InvokeAsync(StateHasChanged);
    }
    [JSInvokable] public async Task OnCanvasDeselected()
    {
        SelectedNodeId = null; SelectedEdgeId = null; await InvokeAsync(StateHasChanged);
    }
    [JSInvokable] public async Task OnNodeMoved(string id, double x, double y)
    {
        if (IsRunning || !double.IsFinite(x) || !double.IsFinite(y)) return;
        var node = Graph.Nodes.FirstOrDefault(n => n.Id == id);
        if (node is null) return;
        node.X = Math.Round(x); node.Y = Math.Round(y);
        await PersistAsync(); await InvokeAsync(StateHasChanged);
    }
    [JSInvokable] public async Task OnConnect(string from, string output, string to, string input)
    {
        if (IsRunning) return;
        var source = Graph.Nodes.FirstOrDefault(n => n.Id == from);
        var dest = Graph.Nodes.FirstOrDefault(n => n.Id == to);
        if (source is null || dest is null || from == to ||
            !NodeCatalog.Types[source.Type].Outputs.Any(p => p.Id == output) ||
            !NodeCatalog.Types[dest.Type].Inputs.Any(p => p.Id == input))
        { await NotifyAsync("Incompatible or self-referencing connection"); return; }
        if (Graph.Edges.Any(e => e.From == from && e.Out == output && e.To == to && e.In == input)) return;
        Graph.Edges.Add(new(NewId("edge"), from, output, to, input));
        await PersistAsync(); await InvokeAsync(StateHasChanged); await NotifyAsync("Nodes connected");
    }
    [JSInvokable] public Task OnDeleteRequested() => DeleteSelectedAsync();
    [JSInvokable] public Task OnRunRequested() => RunDefaultAsync();
    [JSInvokable] public Task OnExportRequested() => ExportAsync();
    [JSInvokable] public Task OnSaveRequested() => SaveCurrentAsync(true);
    [JSInvokable] public Task OnExternalRun(string? nodeId, string? input) =>
        RunSelectedAsync(nodeId ?? Graph.Nodes.FirstOrDefault(n => n.Type is "trigger" or "action-event")?.Id ?? "", input);
    [JSInvokable] public Task<WorkflowGraph> OnGetGraph() => Task.FromResult(Graph.Copy());
    [JSInvokable] public Task<string> OnGetWorkflowId() => Task.FromResult(Current.Id);

    public async ValueTask DisposeAsync()
    {
        _runCancellation?.Cancel(); _interopRef?.Dispose();
        if (_initialized)
        {
            try { await JS.InvokeVoidAsync("nodalCanvas.dispose", "viewport"); }
            catch (JSDisconnectedException) { }
        }
    }
    private sealed record ConsoleLogLine(int Sequence, string Time, string Type, string Text);
}
