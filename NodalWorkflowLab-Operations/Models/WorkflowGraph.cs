using System.Text.Json;
using System.Text.Json.Serialization;

namespace NodalWorkflowLab.Models;

public sealed class WorkflowGraph
{
    [JsonPropertyName("nodes")]
    public List<WorkflowNode> Nodes { get; set; } = [];

    [JsonPropertyName("edges")]
    public List<WorkflowEdge> Edges { get; set; } = [];

    public WorkflowGraph Copy() => JsonSerializer.Deserialize<WorkflowGraph>(JsonSerializer.Serialize(this))!;

    public static WorkflowGraph Example() => new()
    {
        Nodes =
        [
            WorkflowNode.Create("start", "trigger", 80, 290, ("message", "Hello from Nodal!")),
            WorkflowNode.Create("template", "text", 375, 290, ("template", "Received: {{input}}")),
            WorkflowNode.Create("uppercase", "transform", 670, 290, ("operation", "uppercase")),
            WorkflowNode.Create("branch", "condition", 970, 290, ("operator", "includes"), ("value", "HELLO")),
            WorkflowNode.Create("yes-log", "log", 1270, 145, ("label", "Matched HELLO")),
            WorkflowNode.Create("no-log", "log", 1270, 450, ("label", "No match")),
            WorkflowNode.Create("done", "finish", 1570, 295, ("label", "Workflow complete"))
        ],
        Edges =
        [
            new("e1", "start", "next", "template", "in"),
            new("e2", "template", "next", "uppercase", "in"),
            new("e3", "uppercase", "next", "branch", "in"),
            new("e4", "branch", "true", "yes-log", "in"),
            new("e5", "branch", "false", "no-log", "in"),
            new("e6", "yes-log", "next", "done", "in"),
            new("e7", "no-log", "next", "done", "in")
        ]
    };

    public bool Validate(out string problem)
    {
        problem = "";
        if (Nodes is null || Edges is null || Nodes.Count > 300 || Edges.Count > 1000)
        {
            problem = "Invalid graph size (maximum 300 nodes and 1,000 edges).";
            return false;
        }

        if (Nodes.Any(n => n is null || string.IsNullOrWhiteSpace(n.Id) ||
                           !NodeCatalog.Types.ContainsKey(n.Type) || n.Config is null ||
                           !double.IsFinite(n.X) || !double.IsFinite(n.Y)) ||
            Nodes.Select(n => n.Id).Distinct(StringComparer.Ordinal).Count() != Nodes.Count)
        {
            problem = "Graph contains invalid or duplicate nodes.";
            return false;
        }

        if (Edges.Any(e => e is null || string.IsNullOrWhiteSpace(e.Id)) ||
            Edges.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count() != Edges.Count)
        {
            problem = "Graph contains invalid or duplicate edges.";
            return false;
        }

        var byId = Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        foreach (var edge in Edges)
        {
            if (!byId.TryGetValue(edge.From, out var source) ||
                !byId.TryGetValue(edge.To, out var destination) ||
                !NodeCatalog.Types[source.Type].Outputs.Any(p => p.Id == edge.Out) ||
                !NodeCatalog.Types[destination.Type].Inputs.Any(p => p.Id == edge.In))
            {
                problem = "Graph contains an invalid connection/socket.";
                return false;
            }
        }
        return true;
    }
}

public sealed class WorkflowNode
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
    [JsonPropertyName("config")] public Dictionary<string, JsonElement> Config { get; set; } = [];

    public string ConfigText(string key, string fallback = "")
    {
        if (!Config.TryGetValue(key, out var value)) return fallback;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? fallback,
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToString(),
            _ => fallback
        };
    }

    public void SetConfig(string key, string value) => Config[key] = JsonSerializer.SerializeToElement(value);

    public static WorkflowNode Create(string id, string type, double x, double y, params (string Key, string Value)[] fields)
    {
        var node = new WorkflowNode { Id = id, Type = type, X = x, Y = y };
        foreach (var field in fields) node.SetConfig(field.Key, field.Value);
        return node;
    }
}

public sealed record WorkflowEdge(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("from")] string From,
    [property: JsonPropertyName("out")] string Out,
    [property: JsonPropertyName("to")] string To,
    [property: JsonPropertyName("in")] string In);

public sealed class GraphPosition
{
    public double X { get; set; }
    public double Y { get; set; }
}
