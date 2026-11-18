using System.Text.Json;

namespace NodalWorkflowLab.Models;

// All records in this demo are synthetic. These contracts are intentionally independent
// of the node editor so they can later be adapted to actual application modules.
public sealed class Workspace
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? ParentId { get; set; }
    public string Code { get; set; } = "";
}

public sealed class OperationsUser
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Team { get; set; } = "";
}

public sealed class RaciAssignment
{
    public string PrincipalKind { get; set; } = "user"; // user | workspace
    public string PrincipalId { get; set; } = "";
    public string Role { get; set; } = "Responsible";
}

public sealed class ActionItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
    public string EngineReference { get; set; } = "";
    public string OwnerWorkspaceId { get; set; } = "";
    public string? AssignedWorkspaceId { get; set; }
    public string Status { get; set; } = "Open";
    public string Priority { get; set; } = "Normal";
    // Optional typed action contract; older JSON records deserialize with safe empty defaults.
    public string ModuleId { get; set; } = "";
    public string CapabilityId { get; set; } = "";
    public string SourceRecordId { get; set; } = "";
    public string CompletionEventType { get; set; } = "";
    public DateTimeOffset? DueUtc { get; set; }
    public bool RequiresVerifiedCompletion => !string.IsNullOrEmpty(CompletionEventType);

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<RaciAssignment> Assignments { get; set; } = [];
}

public sealed class OperationsSnapshot
{
    public List<Workspace> Workspaces { get; set; } = [];
    public List<OperationsUser> Users { get; set; } = [];
    public List<ActionItem> Actions { get; set; } = [];

    public OperationsSnapshot Copy() => JsonSerializer.Deserialize<OperationsSnapshot>(JsonSerializer.Serialize(this))!;

    public static OperationsSnapshot Example()
    {
        var now = DateTimeOffset.UtcNow;
        return new OperationsSnapshot
        {
            Workspaces =
            [
                new() { Id="cec", Name="Columbus Engine Center", Code="CEC" },
                new() { Id="ops", Name="Engine Operations", ParentId="cec", Code="OPS" },
                new() { Id="induction", Name="Induction", ParentId="ops", Code="IND" },
                new() { Id="disassembly", Name="Disassembly", ParentId="ops", Code="DIS" },
                new() { Id="assembly", Name="Assembly & Test", ParentId="ops", Code="ASM" },
                new() { Id="engineering", Name="Repair Engineering", ParentId="cec", Code="ENG" },
                new() { Id="quality", Name="Quality & Airworthiness", ParentId="cec", Code="QAL" },
                new() { Id="supply", Name="Supply Chain", ParentId="cec", Code="SUP" },
                new() { Id="materials", Name="Materials Planning", ParentId="supply", Code="MAT" },
                new() { Id="programs", Name="Programs & Scheduling", ParentId="cec", Code="PGM" }
            ],
            Users =
            [
                new() { Id="u-alex", Name="Alex Morgan", Team="Induction" },
                new() { Id="u-jordan", Name="Jordan Lee", Team="Repair Engineering" },
                new() { Id="u-sam", Name="Sam Patel", Team="Quality" },
                new() { Id="u-casey", Name="Casey Brooks", Team="Materials" },
                new() { Id="u-taylor", Name="Taylor Reed", Team="Assembly & Test" }
            ],
            Actions =
            [
                A("ACT-1001", "Confirm incoming engine documentation", "induction", "PW-DEMO-1007", "In Progress", "High", now.AddDays(-2), "u-alex"),
                A("ACT-1002", "Resolve seal inspection disposition", "disassembly", "PW-DEMO-1007", "Blocked", "Critical", now.AddDays(-4), "u-jordan"),
                A("ACT-1003", "Review repair procedure revision", "engineering", "PW-DEMO-1011", "Open", "High", now.AddDays(-1), "u-jordan"),
                A("ACT-1004", "Coordinate replacement hardware", "materials", "PW-DEMO-1007", "In Progress", "High", now.AddDays(-2), "u-casey"),
                A("ACT-1005", "Complete post-repair evidence package", "quality", "PW-DEMO-0998", "Done", "Normal", now.AddDays(-7), "u-sam"),
                A("ACT-1006", "Approve assembly readiness checklist", "assembly", "PW-DEMO-1011", "Open", "Normal", now.AddDays(-1), "u-taylor"),
                A("ACT-1007", "Reconcile shop visit milestone", "programs", "PW-DEMO-1007", "In Progress", "Normal", now.AddDays(-3), "u-alex"),
                A("ACT-1008", "Disposition dimensional inspection finding", "quality", "PW-DEMO-1011", "Blocked", "High", now.AddDays(-4), "u-sam"),
            ]
        };
    }

    private static ActionItem A(string id, string title, string workspace, string engine, string status,
        string priority, DateTimeOffset updated, string user) => new()
        {
            Id = id, Title = title, OwnerWorkspaceId = workspace, AssignedWorkspaceId = workspace,
            EngineReference = engine, Status = status, Priority = priority,
            CreatedUtc = updated.AddDays(-1), UpdatedUtc = updated,
            Assignments =
            [
                new() { PrincipalKind = "user", PrincipalId = user, Role = "Responsible" },
                new() { PrincipalKind = "workspace", PrincipalId = workspace, Role = "Accountable" }
            ]
        };
}

public sealed record WorkspaceSummary(string WorkspaceId, string WorkspaceName, int Direct, int Total,
    int Open, int InProgress, int Blocked, int Done, int Critical, int DescendantCount)
{
    public int Active => Open + InProgress + Blocked;
    public int CompletionPercent => Total == 0 ? 0 : (int)Math.Round(Done * 100.0 / Total);
}

public static class OperationsRollups
{
    public static HashSet<string> DescendantIds(OperationsSnapshot data, string id)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal) { id };
        var queue = new Queue<string>();
        queue.Enqueue(id);
        while (queue.Count > 0)
        {
            var parent = queue.Dequeue();
            foreach (var child in data.Workspaces.Where(w => w.ParentId == parent))
                if (ids.Add(child.Id)) queue.Enqueue(child.Id);
        }
        return ids;
    }

    public static List<ActionItem> ActionsFor(OperationsSnapshot data, string workspaceId, bool recursive = true)
    {
        var ids = recursive ? DescendantIds(data, workspaceId) : new HashSet<string>(StringComparer.Ordinal) { workspaceId };
        // Assignment and ownership are both ways a workspace participates in an item.
        // A distinct action item counts only once within a single rollup.
        return data.Actions.Where(a => ids.Contains(a.OwnerWorkspaceId) ||
                                       (a.AssignedWorkspaceId is not null && ids.Contains(a.AssignedWorkspaceId)) ||
                                       a.Assignments.Any(r => r.PrincipalKind == "workspace" && ids.Contains(r.PrincipalId)))
            .DistinctBy(a => a.Id).ToList();
    }

    public static WorkspaceSummary Summarize(OperationsSnapshot data, string workspaceId)
    {
        var workspace = data.Workspaces.FirstOrDefault(w => w.Id == workspaceId);
        if (workspace is null) throw new ArgumentException("Workspace not found", nameof(workspaceId));
        var items = ActionsFor(data, workspaceId);
        var direct = ActionsFor(data, workspaceId, false).Count;
        return new(workspaceId, workspace.Name, direct, items.Count,
            items.Count(a => a.Status == "Open"),
            items.Count(a => a.Status == "In Progress"),
            items.Count(a => a.Status == "Blocked"),
            items.Count(a => a.Status == "Done"),
            items.Count(a => a.Priority == "Critical"),
            DescendantIds(data, workspaceId).Count - 1);
    }
}

public sealed record ActionEvent(string Kind, string ActionId, string? WorkspaceId, string Message = "");
