namespace NodalWorkflowLab.Models;

public sealed record SocketDefinition(string Id, string Label, string Color);
public sealed record ConfigOption(string Value, string Label);
public sealed record ConfigField(string Key, string Label, string Kind, ConfigOption[]? Options = null);
public sealed record NodeDefinition(string Id, string Title, string Category, string Icon, string Color,
    string Description, SocketDefinition[] Inputs, SocketDefinition[] Outputs, ConfigField[] Fields);

public static class NodeCatalog
{
    private static readonly SocketDefinition Input = new("in", "Event", "#9daec7");
    private static readonly SocketDefinition Next = new("next", "Next", "#90b6eb");
    private static readonly SocketDefinition[] In = [Input];
    private static readonly SocketDefinition[] Out = [Next];
    private static ConfigField Select(string key, string label, params string[] items) => new(key, label, "select",
        items.Select(s => new ConfigOption(s, s)).ToArray());

    public static IReadOnlyDictionary<string, NodeDefinition> Types { get; } =
        new Dictionary<string, NodeDefinition>(StringComparer.Ordinal)
        {
            ["trigger"] = new("trigger", "Manual Trigger", "Triggers", "⚡", "#e7bb75", "Start a workflow manually.", [], Out,
                [new("message", "Event payload", "textarea")]),
            ["action-event"] = new("action-event", "Action Item Event", "Triggers", "◉", "#e7bb75",
                "Start when an action item is created, changed or routed.", [], Out,
                [Select("eventType", "When", "status-changed", "created", "assigned", "any"),
                 new("workspaceId", "Restrict to workspace (optional)", "workspace-optional")]),
            ["load-action"] = new("load-action", "Load Action Item", "Operations", "▤", "#59cadd",
                "Read the current action item into the event payload.", In, Out, []),
            ["action-status"] = new("action-status", "Check Action Status", "Operations", "◇", "#79aeff",
                "Branch on the current action status.", In,
                [new("true", "Match", "#65d7ab"), new("false", "Other", "#e7ab72")],
                [Select("status", "Required status", OperationsStoreStatuses.Open, OperationsStoreStatuses.InProgress,
                    OperationsStoreStatuses.Blocked, OperationsStoreStatuses.Done)]),
            ["route-workspace"] = new("route-workspace", "Route to Workspace", "Operations", "⇢", "#ffbf83",
                "Assign the current action item to a workspace.", In, Out,
                [new("workspaceId", "Destination workspace", "workspace")]),
            ["assign-raci"] = new("assign-raci", "Assign RACI", "Operations", "♧", "#8bd3b0",
                "Add a Responsible, Accountable, Consulted or Informed principal.", In, Out,
                [Select("principalKind", "Principal type", "user", "workspace"),
                 new("principalId", "Principal ID", "principal"),
                 Select("role", "Assignment role", "Responsible", "Accountable", "Consulted", "Informed")]),
            ["set-status"] = new("set-status", "Set Action Status", "Operations", "✓", "#91d49b",
                "Update the status of the current action item.", In, Out,
                [Select("status", "New status", OperationsStoreStatuses.Open, OperationsStoreStatuses.InProgress,
                    OperationsStoreStatuses.Blocked, OperationsStoreStatuses.Done)]),
            ["create-action"] = new("create-action", "Create Action Item", "Operations", "+", "#81d1c3",
                "Create a follow-up action in a selected workspace.", In, Out,
                [new("title", "Follow-up title", "text"), new("workspaceId", "Owning workspace", "workspace"),
                 Select("priority", "Priority", "Low", "Normal", "High", "Critical")]),
            ["workspace-rollup"] = new("workspace-rollup", "Workspace Rollup", "Operations", "▦", "#b4a0ff",
                "Return recursive dashboard metrics for a workspace.", In, Out,
                [new("workspaceId", "Dashboard workspace", "workspace")]),
            ["notify"] = new("notify", "Notify (Simulation)", "Outputs", "✉", "#e5be90",
                "Emit an example notification; does not send email.", In, Out,
                [new("message", "Notification message", "textarea")]),
            ["text"] = new("text", "Text Template", "Data", "T", "#7cc9ec", "Replace {{input}} and action-item tokens.", In, Out,
                [new("template", "Text template", "textarea")]),
            ["transform"] = new("transform", "Transform Text", "Data", "⇄", "#b29cff", "Modify the text payload.", In, Out,
                [Select("operation", "Operation", "uppercase", "lowercase", "trim", "reverse")]),
            ["condition"] = new("condition", "Condition", "Logic", "◇", "#88adff", "Branch based on event text.", In,
                [new("true", "True", "#65d7ab"), new("false", "False", "#e7ab72")],
                [Select("operator", "Comparison", "includes", "equals", "starts", "greater"), new("value", "Compare against", "text")]),
            ["subworkflow"] = new("subworkflow", "Invoke Subworkflow", "Composition", "⬡", "#c0a0ff",
                "Run a reusable workflow as one node. Child logs appear in the parent console.", In, Out,
                [new("workflowId", "Workflow ID", "text")]),
            ["site-query"] = new("site-query", "Query Site Module", "Site Modules", "◎", "#72c3de",
                "Read synthetic engine, part or waiting-execution state without changing regulated records.", In, Out,
                [Select("query", "Allowed query", "engine-summary", "part-status", "waiting-executions"),
                 new("engineId", "Synthetic engine ID", "text")]),
            ["delay"] = new("delay", "Wait", "Logic", "◷", "#c29bfa", "Delay an event by up to five seconds.", In, Out,
                [new("ms", "Delay (milliseconds)", "number")]),
            ["log"] = new("log", "Console Log", "Outputs", "≡", "#68d2ba", "Write to the event console.", In, Out,
                [new("label", "Log label", "text")]),
            ["finish"] = new("finish", "Finish", "Outputs", "✓", "#a5df91", "Complete a workflow branch.", In, [],
                [new("label", "Result label", "text")])
        };

    public static string Preview(WorkflowNode node) => node.Type switch
    {
        "trigger" => node.ConfigText("message"),
        "action-event" => "On: " + node.ConfigText("eventType", "status-changed"),
        "load-action" => "Current action → JSON",
        "action-status" or "set-status" => "Status: " + node.ConfigText("status"),
        "route-workspace" or "workspace-rollup" => "Workspace: " + node.ConfigText("workspaceId"),
        "assign-raci" => $"{node.ConfigText("role")} → {node.ConfigText("principalId")}",
        "create-action" => node.ConfigText("title"),
        "notify" => "Simulation: " + node.ConfigText("message"),
        "text" => node.ConfigText("template"),
        "transform" => node.ConfigText("operation"),
        "condition" => $"{node.ConfigText("operator")} “{node.ConfigText("value")}”",
        "delay" => node.ConfigText("ms", "450") + " ms",
        "subworkflow" => "Calls: " + node.ConfigText("workflowId"),
        "site-query" => node.ConfigText("query") + " · " + node.ConfigText("engineId"),
        "log" or "finish" => node.ConfigText("label"),
        _ => ""
    };
}

internal static class OperationsStoreStatuses
{
    public const string Open = "Open";
    public const string InProgress = "In Progress";
    public const string Blocked = "Blocked";
    public const string Done = "Done";
}
