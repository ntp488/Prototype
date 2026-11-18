using System.Text.Json;

namespace NodalWorkflowLab.Models;

public sealed class WorkflowDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Untitled Workflow";
    public string Description { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public WorkflowGraph Graph { get; set; } = new();
    public WorkflowDocument Copy() => JsonSerializer.Deserialize<WorkflowDocument>(JsonSerializer.Serialize(this))!;
}

public static class WorkflowTemplates
{
    public static List<WorkflowDocument> Create() =>
    [
        new()
        {
            Id = "blocked-escalation", Name = "Blocked item escalation", Enabled = true,
            Description = "On a status change, route blocked action items to Repair Engineering and assign a responsible engineer.",
            Graph = new()
            {
                Nodes =
                [
                    WorkflowNode.Create("blocked-event", "action-event", 70, 230, ("eventType", "status-changed")),
                    WorkflowNode.Create("blocked-check", "action-status", 360, 230, ("status", "Blocked")),
                    WorkflowNode.Create("blocked-route", "route-workspace", 650, 120, ("workspaceId", "engineering")),
                    WorkflowNode.Create("blocked-raci", "assign-raci", 945, 120, ("principalKind", "user"), ("principalId", "u-jordan"), ("role", "Responsible")),
                    WorkflowNode.Create("blocked-note", "notify", 1240, 120, ("message", "Escalation queued for {{actionTitle}}")),
                    WorkflowNode.Create("blocked-end", "finish", 1525, 120, ("label", "Escalation complete")),
                    WorkflowNode.Create("blocked-skip", "finish", 665, 430, ("label", "Action not blocked"))
                ],
                Edges =
                [
                    new("blocked-e1", "blocked-event", "next", "blocked-check", "in"),
                    new("blocked-e2", "blocked-check", "true", "blocked-route", "in"),
                    new("blocked-e3", "blocked-route", "next", "blocked-raci", "in"),
                    new("blocked-e4", "blocked-raci", "next", "blocked-note", "in"),
                    new("blocked-e5", "blocked-note", "next", "blocked-end", "in"),
                    new("blocked-e6", "blocked-check", "false", "blocked-skip", "in")
                ]
            }
        },
        new()
        {
            Id = "quality-followup", Name = "Completion → quality review", Enabled = true,
            Description = "When an action item is completed, create a downstream quality-review action in the Quality workspace.",
            Graph = new()
            {
                Nodes =
                [
                    WorkflowNode.Create("quality-event", "action-event", 75, 240, ("eventType", "status-changed")),
                    WorkflowNode.Create("quality-check", "action-status", 365, 240, ("status", "Done")),
                    WorkflowNode.Create("quality-create", "create-action", 665, 125, ("title", "Review evidence: {{actionTitle}}"), ("workspaceId", "quality"), ("priority", "Normal")),
                    WorkflowNode.Create("quality-note", "notify", 965, 125, ("message", "Quality review created: {{actionTitle}}")),
                    WorkflowNode.Create("quality-end", "finish", 1265, 125, ("label", "Review requested")),
                    WorkflowNode.Create("quality-skip", "finish", 665, 430, ("label", "Not complete"))
                ],
                Edges =
                [
                    new("quality-e1", "quality-event", "next", "quality-check", "in"),
                    new("quality-e2", "quality-check", "true", "quality-create", "in"),
                    new("quality-e3", "quality-create", "next", "quality-note", "in"),
                    new("quality-e4", "quality-note", "next", "quality-end", "in"),
                    new("quality-e5", "quality-check", "false", "quality-skip", "in")
                ]
            }
        },
        new()
        {
            Id = "workspace-rollup", Name = "Organization rollup snapshot", Enabled = false,
            Description = "Manually fetch a recursive workspace dashboard and log its metrics.",
            Graph = new()
            {
                Nodes =
                [
                    WorkflowNode.Create("rollup-start", "trigger", 100, 240, ("message", "Rollup requested")),
                    WorkflowNode.Create("rollup-summary", "workspace-rollup", 420, 240, ("workspaceId", "cec")),
                    WorkflowNode.Create("rollup-log", "log", 740, 240, ("label", "CEC dashboard")),
                    WorkflowNode.Create("rollup-end", "finish", 1060, 240, ("label", "Rollup complete"))
                ],
                Edges =
                [
                    new("rollup-e1", "rollup-start", "next", "rollup-summary", "in"),
                    new("rollup-e2", "rollup-summary", "next", "rollup-log", "in"),
                    new("rollup-e3", "rollup-log", "next", "rollup-end", "in")
                ]
            }
        },
        ..ProcessWorkflowTemplates.Create()
    ];
}
