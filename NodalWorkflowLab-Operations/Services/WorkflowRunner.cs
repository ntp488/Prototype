using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using NodalWorkflowLab.Models;

namespace NodalWorkflowLab.Services;

/// <summary>
/// Deterministic, bounded execution of trusted, allowlisted node types.
/// Data writes go through OperationsStore. The prototype does not support scripts, arbitrary SQL, or HTTP calls.
/// </summary>
public sealed class WorkflowRunner(OperationsStore operations, WorkflowLibraryStore library, ProcessOperationsStore processes)
{
    public async Task RunAsync(WorkflowGraph graph, string startId, WorkflowInput input,
        Func<WorkflowEvent, Task> publish, CancellationToken cancellationToken = default)
    {
        _ = await RunInternalAsync(graph, startId, input, publish, cancellationToken, new HashSet<string>(StringComparer.Ordinal), 0);
    }

    private async Task<string> RunInternalAsync(WorkflowGraph graph, string startId, WorkflowInput input,
        Func<WorkflowEvent, Task> publish, CancellationToken cancellationToken,
        HashSet<string> callStack, int depth)
    {
        if (depth > 7) throw new InvalidOperationException("Subworkflow depth exceeded (maximum 7).");
        if (!graph.Validate(out var problem)) throw new InvalidDataException(problem);
        var byId = graph.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        if (!byId.TryGetValue(startId, out var start)) throw new ArgumentException("Starting node does not exist.");
        var queue = new Queue<(string NodeId, string Payload, string? ActionId)>();
        queue.Enqueue((startId, input.Payload ?? "", input.ActionId));
        var watch = Stopwatch.StartNew();
        var count = 0;
        var lastPayload = input.Payload ?? "";
        await publish(new("message", Text: $"Execution started: {NodeCatalog.Types[start.Type].Title} · item {input.ActionId ?? "none"}"));

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++count > 80) throw new InvalidOperationException("Safety limit reached (80 visits); check for a cycle.");
            var (id, payload, actionId) = queue.Dequeue();
            if (!byId.TryGetValue(id, out var node)) continue;
            await publish(new("node-start", id, payload));
            await Task.Delay(90, cancellationToken);
            var result = await ExecuteNodeAsync(node, payload, actionId, input, publish, cancellationToken, callStack, depth);
            lastPayload = result.Payload;
            await publish(new("node-complete", id, result.Payload));
            foreach (var port in result.OutputPorts)
            {
                await publish(new("node-output", id, result.Payload, port));
                foreach (var edge in graph.Edges.Where(e => e.From == node.Id && e.Out == port))
                {
                    await publish(new("edge-traverse", id, result.Payload, port,
                        $"{NodeCatalog.Types[node.Type].Title} → {NodeCatalog.Types[byId[edge.To].Type].Title} [{port}]", edge.Id));
                    await Task.Delay(100, cancellationToken);
                    await publish(new("edge-idle", id, EdgeId: edge.Id));
                    queue.Enqueue((edge.To, result.Payload, result.ActionId ?? actionId));
                }
            }
            await Task.Delay(45, cancellationToken);
        }
        watch.Stop();
        await publish(new("run-complete", Text: $"Completed · {count} node visits · {watch.ElapsedMilliseconds} ms"));
        return lastPayload;
    }

    private async Task<NodeResult> ExecuteNodeAsync(WorkflowNode node, string input, string? actionId,
        WorkflowInput context, Func<WorkflowEvent, Task> publish, CancellationToken cancellationToken,
        HashSet<string> callStack, int depth)
    {
        string C(string name, string fallback = "") => node.ConfigText(name, fallback);
        ActionItem? action = null;
        OperationsSnapshot? snapshot = null;
        if (actionId is not null)
        {
            snapshot = await operations.SnapshotAsync();
            action = snapshot.Actions.FirstOrDefault(a => a.Id == actionId);
        }
        string Expand(string template) => template
            .Replace("{{input}}", input, StringComparison.Ordinal)
            .Replace("{{actionId}}", action?.Id ?? "", StringComparison.Ordinal)
            .Replace("{{actionTitle}}", action?.Title ?? "", StringComparison.Ordinal)
            .Replace("{{status}}", action?.Status ?? "", StringComparison.Ordinal)
            .Replace("{{engine}}", action?.EngineReference ?? "", StringComparison.Ordinal);
        ActionItem RequireAction() => action ?? throw new InvalidOperationException(
            "This node requires an action item. Choose a test action in the inspector or use an action event trigger.");
        switch (node.Type)
        {
            case "site-query":
            {
                var engineId = C("engineId", action?.EngineReference ?? "PW-DEMO-1007");
                var state = await processes.SnapshotAsync();
                if (!state.Engines.Any(e => e.Id == engineId))
                    throw new ArgumentException("Unknown synthetic engine: " + engineId);
                var value = C("query", "engine-summary") switch
                {
                    "engine-summary" => JsonSerializer.Serialize(state.Engines.First(e => e.Id == engineId)),
                    "part-status" => JsonSerializer.Serialize(state.Parts.Where(p => p.EngineId == engineId)),
                    "waiting-executions" => JsonSerializer.Serialize(state.Runs.Where(r => r.EngineId == engineId && r.Status == "Waiting")),
                    _ => throw new ArgumentException("Unknown site query")
                };
                return new(["next"], value);
            }
            case "subworkflow":
            {
                var workflowId = C("workflowId");
                if (string.IsNullOrWhiteSpace(workflowId)) throw new InvalidOperationException("Choose a workflow ID to invoke.");
                if (callStack.Contains(workflowId)) throw new InvalidOperationException("Recursive workflow reference detected: " + workflowId);
                var child = await library.GetAsync(workflowId) ?? throw new InvalidOperationException("Subworkflow was not found: " + workflowId);
                var entry = child.Graph.Nodes.FirstOrDefault(n => n.Type is "trigger" or "action-event") ?? child.Graph.Nodes.FirstOrDefault();
                if (entry is null) throw new InvalidOperationException("The selected subworkflow has no nodes.");
                var nextStack = new HashSet<string>(callStack, StringComparer.Ordinal) { workflowId };
                await publish(new("message", node.Id, Text: $"↳ Entering reusable workflow: {child.Name}"));
                var result = await RunInternalAsync(child.Graph, entry.Id,
                    new WorkflowInput(input, actionId, context.EventKind),
                    async e =>
                    {
                        if (e.Type == "message" || e.Type == "run-error")
                            await publish(new("message", node.Id, e.Payload, Text: $"  {child.Name}: {e.Text}"));
                    }, cancellationToken, nextStack, depth + 1);
                await publish(new("message", node.Id, Text: $"↰ Returned from: {child.Name}"));
                return new(["next"], result, actionId);
            }
            case "trigger":
                return new(["next"], string.IsNullOrEmpty(input) ? C("message", "Manual event") : input);
            case "action-event":
            {
                var wanted = C("eventType", "status-changed");
                if (context.EventKind is not null && wanted != "any" && wanted != context.EventKind)
                    return new([], input);
                var scoped = C("workspaceId");
                if (!string.IsNullOrEmpty(scoped) && action is not null)
                {
                    snapshot ??= await operations.SnapshotAsync();
                    var ids = OperationsRollups.DescendantIds(snapshot, scoped);
                    if (!ids.Contains(action.OwnerWorkspaceId) &&
                        (action.AssignedWorkspaceId is null || !ids.Contains(action.AssignedWorkspaceId)))
                        return new([], input);
                }
                return new(["next"], action is null ? input : $"{context.EventKind ?? "test"}: {action.Id} · {action.Title}");
            }
            case "text":
                return new(["next"], Expand(C("template", "{{input}}")));
            case "transform":
            {
                var transformed = C("operation", "uppercase") switch
                {
                    "uppercase" => input.ToUpperInvariant(),
                    "lowercase" => input.ToLowerInvariant(),
                    "trim" => input.Trim(),
                    "reverse" => ReverseUnicode(input),
                    _ => input
                };
                return new(["next"], transformed);
            }
            case "condition":
            {
                var expected = Expand(C("value"));
                var passes = C("operator", "includes") switch
                {
                    "equals" => input.Equals(expected, StringComparison.Ordinal),
                    "starts" => input.StartsWith(expected, StringComparison.Ordinal),
                    "greater" => double.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && input.Length > n,
                    _ => input.Contains(expected, StringComparison.Ordinal)
                };
                return new([passes ? "true" : "false"], input);
            }
            case "action-status":
            {
                var current = RequireAction();
                return new([current.Status == C("status", "Blocked") ? "true" : "false"],
                    $"{current.Id}: {current.Status}");
            }
            case "load-action":
            {
                var current = RequireAction();
                return new(["next"], JsonSerializer.Serialize(current));
            }
            case "route-workspace":
            {
                var current = RequireAction();
                var targetId = C("workspaceId", "engineering");
                var modified = await operations.RouteAsync(current.Id, targetId);
                await publish(new("message", node.Id, Text: $"ROUTED {modified.Id} → {targetId}"));
                return new(["next"], $"Routed {modified.Id} to {targetId}");
            }
            case "set-status":
            {
                var current = RequireAction();
                var modified = await operations.SetStatusAsync(current.Id, C("status", "In Progress"));
                await publish(new("message", node.Id, Text: $"STATUS {modified.Id} → {modified.Status}"));
                return new(["next"], $"{modified.Id} status: {modified.Status}");
            }
            case "assign-raci":
            {
                var current = RequireAction();
                var modified = await operations.AssignAsync(current.Id, C("principalKind", "user"),
                    C("principalId", "u-jordan"), C("role", "Responsible"));
                await publish(new("message", node.Id, Text: $"RACI updated for {modified.Id}"));
                return new(["next"], $"RACI assignment recorded: {modified.Id}");
            }
            case "create-action":
            {
                var newTitle = Expand(C("title", "Workflow follow-up"));
                var created = await operations.CreateActionAsync(newTitle, C("workspaceId", "quality"),
                    C("priority", "Normal"), engineReference: action?.EngineReference ?? "");
                await publish(new("message", node.Id, Text: $"CREATED {created.Id}: {created.Title}"));
                return new(["next"], $"Created {created.Id}: {created.Title}", created.Id);
            }
            case "workspace-rollup":
            {
                var data = await operations.SnapshotAsync();
                var rollup = OperationsRollups.Summarize(data, C("workspaceId", "cec"));
                return new(["next"], JsonSerializer.Serialize(rollup));
            }
            case "notify":
            {
                var message = Expand(C("message", "Workflow notification"));
                await publish(new("message", node.Id, Text: "SIMULATED NOTICE · " + message));
                return new(["next"], message);
            }
            case "delay":
            {
                _ = double.TryParse(C("ms", "450"), NumberStyles.Float, CultureInfo.InvariantCulture, out var configured);
                await Task.Delay((int)Math.Clamp(double.IsFinite(configured) ? configured : 0, 0, 5000), cancellationToken);
                return new(["next"], input);
            }
            case "log":
                await publish(new("message", node.Id, input, Text: $"{C("label", "Log")}: {input}"));
                return new(["next"], input);
            case "finish":
                await publish(new("message", node.Id, input, Text: $"{C("label", "Finished")}: {input}"));
                return new([], input);
            default:
                throw new InvalidOperationException("Unknown node type: " + node.Type);
        }
    }

    private static string ReverseUnicode(string input)
    {
        var runes = new List<Rune>();
        foreach (var rune in input.EnumerateRunes()) runes.Add(rune);
        runes.Reverse();
        return string.Concat(runes.Select(r => r.ToString()));
    }
}

public sealed record WorkflowInput(string? Payload = null, string? ActionId = null, string? EventKind = null);
public sealed record NodeResult(string[] OutputPorts, string Payload, string? ActionId = null);
public sealed record WorkflowEvent(string Type, string? NodeId = null, string? Payload = null,
    string? Port = null, string? Text = null, string? EdgeId = null);
