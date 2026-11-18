namespace NodalWorkflowLab.Models;

/// <summary>
/// Read-only, computed process projections over observed synthetic records.
/// No joins infer a regulatory sign-off; a missing child record remains "Untracked".
/// </summary>
public static class FleetAnalytics
{
    public static string Status(EngineCase engine, ProcessDefinition step, ProcessMapSnapshot map)
    {
        if (step.Id == "repair")
        {
            if (engine.Stages.GetValueOrDefault("release") == "Complete") return "Complete";
            if (engine.Stages.Values.Any(s => s == "Blocked")) return "Blocked";
            if (engine.Stages.Values.Any(s => s == "Waiting")) return "Waiting";
            return "In progress";
        }
        if (engine.Stages.TryGetValue(step.Id, out var observed)) return observed;
        // A missing subprocess observation must never be inferred from the
        // parent process completing: that would fabricate a per-step audit trail.
        var cursor = step.ParentId;
        while (!string.IsNullOrEmpty(cursor))
        {
            if (engine.Stages.TryGetValue(cursor, out var ancestor))
                return ancestor == "Not applicable" ? "Not applicable" : "Untracked";
            cursor = map.Processes.FirstOrDefault(p => p.Id == cursor)?.ParentId;
        }
        return "Untracked";
    }

    public static int Progress(EngineCase engine, ProcessDefinition process, ProcessMapSnapshot map)
    {
        var status = Status(engine, process, map);
        if (status == "Complete") return 100;
        if (status is "Not started" or "Not applicable" or "Untracked") return 0;
        return Math.Clamp(engine.StageProgress.GetValueOrDefault(process.Id), 0, 99);
    }

    public static string ClassName(string status) => status switch
    {
        "Complete" => "complete",
        "Blocked" => "blocked",
        "Waiting" => "waiting",
        "In progress" or "Ready" => "active",
        "Not applicable" => "not-applicable",
        "Untracked" => "untracked",
        _ => "pending"
    };

    public static string CurrentProcess(EngineCase engine) =>
        new[] { "induct", "teardown", "findings", "materials", "quality", "rebuild", "test", "release" }
            .FirstOrDefault(key => engine.Stages.GetValueOrDefault(key) is "In progress" or "Ready" or "Blocked" or "Waiting")
        ?? (engine.Stages.GetValueOrDefault("release") == "Complete" ? "release" : "induct");

    public static FleetSliceSummary Summarize(ProcessDefinition process, IEnumerable<EngineCase> engines, ProcessMapSnapshot map,
        ProcessSnapshot state, OperationsSnapshot actions)
    {
        var rows = engines.Select(e => (Engine: e, Status: Status(e, process, map))).Where(x => x.Status != "Not applicable").ToList();
        var ids = rows.Select(x => x.Engine.Id).ToHashSet(StringComparer.Ordinal);
        var holds = state.Parts.Count(p => ids.Contains(p.EngineId) && p.Held);
        var findings = state.Findings.Count(f => ids.Contains(f.EngineId) && f.Status != "Cleared (demo)");
        var waits = state.Runs.Count(r => ids.Contains(r.EngineId) && r.Status == "Waiting");
        var linkedActions = actions.Actions.Count(a => ids.Contains(a.EngineReference) && a.Status != "Done");
        return new FleetSliceSummary(process.Id, rows.Count,
            rows.Count(x => x.Status == "Complete"), rows.Count(x => x.Status is "In progress" or "Ready"),
            rows.Count(x => x.Status == "Waiting"), rows.Count(x => x.Status == "Blocked"),
            rows.Count(x => x.Status == "Not started"), rows.Count(x => x.Status == "Untracked"),
            holds, findings, waits, linkedActions);
    }

    public static FleetLinkSummary SummarizeLink(ProcessDefinition source, ProcessDefinition target,
        IEnumerable<EngineCase> engines, ProcessMapSnapshot map)
    {
        int relevant = 0, sourceComplete = 0, transitioned = 0, waiting = 0;
        foreach (var engine in engines)
        {
            var from = Status(engine, source, map);
            var to = Status(engine, target, map);
            if (from == "Not applicable" || to == "Not applicable") continue;
            relevant++;
            if (from == "Complete")
            {
                sourceComplete++;
                if (to is "In progress" or "Ready" or "Complete") transitioned++;
            }
            if (from is "Waiting" or "Blocked" || to is "Waiting" or "Blocked") waiting++;
        }
        return new FleetLinkSummary(relevant, sourceComplete, transitioned, waiting);
    }

    public static bool MatchesFilter(EngineCase engine, ProcessSnapshot snapshot, string search,
        string family, string stage, string exceptions)
    {
        var query = search.Trim();
        if (query.Length > 0 && !(engine.Id + " " + engine.WorkOrder + " " + engine.Family + " " +
                                  engine.Cell + " " + engine.Customer + " " + engine.Scope)
            .Contains(query, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrEmpty(family) && engine.Family != family) return false;
        if (!string.IsNullOrEmpty(stage) && CurrentProcess(engine) != stage) return false;
        if (exceptions == "holds" && !snapshot.Parts.Any(p => p.EngineId == engine.Id && p.Held)) return false;
        if (exceptions == "waiting" && !snapshot.Runs.Any(r => r.EngineId == engine.Id && r.Status == "Waiting")) return false;
        if (exceptions == "findings" && !snapshot.Findings.Any(f => f.EngineId == engine.Id && f.Status != "Cleared (demo)")) return false;
        if (exceptions == "overdue" && engine.TargetUtc >= DateTimeOffset.UtcNow) return false;
        return true;
    }
}

public sealed record FleetSliceSummary(string ProcessId, int Total, int Complete, int Active,
    int Waiting, int Blocked, int NotStarted, int Untracked, int Holds, int Findings, int Waits,
    int OpenActions)
{
    public int PercentComplete => Total == 0 ? 0 : (int)Math.Round(Complete * 100.0 / Total);
    public int Exceptions => Waiting + Blocked;
}

public sealed record FleetLinkSummary(int Total, int SourceComplete, int Transitioned, int Exceptions)
{
    public int ConversionPercent => SourceComplete == 0 ? 0 : (int)Math.Round(Transitioned * 100.0 / SourceComplete);
}
