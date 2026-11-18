using System.Text.Json;

namespace NodalWorkflowLab.Models;

// Training-only projections. The event ledger and captured snapshots are not
// authoritative maintenance history, inspection evidence or sign-off records.
public sealed class FleetHistoryIndex
{
    public string Id { get; set; } = "";
    public DateTimeOffset CapturedUtc { get; set; }
    public int VisitCount { get; set; }
    public int HoldCount { get; set; }
    public Dictionary<string,int> Queues { get; set; } = [];
    public Dictionary<string,int> Completed { get; set; } = [];
}
public sealed class FleetHistoryFrame
{
    public DateTimeOffset CapturedUtc { get; set; }
    public ProcessSnapshot Operations { get; set; } = new();
    public OperationsSnapshot Actions { get; set; } = new();
}
public sealed class FleetInvestigationView
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public DateTimeOffset SavedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Scope { get; set; } = "site";
    public string WorkspaceId { get; set; } = "";
    public string FocusEngineId { get; set; } = "";
    public string LevelId { get; set; } = "repair";
    public string ProcessId { get; set; } = "";
    public List<string> SelectedProcesses { get; set; } = [];
    public string MultiMatch { get; set; } = "union";
    public string LinkFrom { get; set; } = "";
    public string LinkTo { get; set; } = "";
    public string Family { get; set; } = "";
    public string Search { get; set; } = "";
    public string Stage { get; set; } = "";
    public string Exception { get; set; } = "";
    public string TableSearch { get; set; } = "";
    public string TableFamily { get; set; } = "";
    public string TableException { get; set; } = "";
    public string Heatmap { get; set; } = "pressure";
    public string SliceStatus { get; set; } = "current";
    public string TableSort { get; set; } = "exceptions";
    public Dictionary<string, FleetLayoutPoint> Layout { get; set; } = [];
    public FleetLayoutPoint ViewCenter { get; set; } = new();
    public double ViewScale { get; set; } = 1;
}
public sealed class FleetLayoutCapture
{
    public Dictionary<string, FleetLayoutPoint> Layout { get; set; } = [];
    public FleetLayoutPoint Center { get; set; } = new();
    public double Scale { get; set; } = 1;
}
public sealed class FleetLayoutPoint
{
    public double X { get; set; }
    public double Y { get; set; }
}
public sealed record FleetBlocker(string EngineId, string Kind, string ProcessId, string Detail);
public sealed record FleetBlockerGroup(string Kind, string ProcessId, int Visits, int ActionCount, string Explanation);
public sealed record FleetForecastRow(string ProcessId, string Name, int Queue, double DailyCapacity,
    double EstimatedDays, int SimulatedNewArrivals, double ScenarioDays, int ChangePercent);

public static class FleetInsights
{
    public static List<FleetBlocker> BlockersFor(EngineCase engine, ProcessSnapshot state, OperationsSnapshot actions)
    {
        var list = new List<FleetBlocker>();
        foreach (var part in state.Parts.Where(p => p.EngineId == engine.Id && p.Held))
            list.Add(new(engine.Id, "Part hold", "materials", $"{part.Id} is held at {part.Location}"));
        foreach (var finding in state.Findings.Where(f => f.EngineId == engine.Id && f.Status != "Cleared (demo)"))
        {
            var quality = finding.Status == "Awaiting inspection";
            list.Add(new(engine.Id, quality ? "Inspection evidence" : "Engineering disposition",
                quality ? "quality" : "findings", $"{finding.Id}: {finding.Status}"));
        }
        foreach (var run in state.Runs.Where(r => r.EngineId == engine.Id && r.Status == "Waiting" &&
                     !state.Findings.Any(f => f.Id == r.FindingId && f.EngineId == engine.Id && f.Status != "Cleared (demo)")))
            list.Add(new(engine.Id, "Workflow wait", "quality", $"{run.Id} awaits {run.WaitingFor}"));
        foreach (var action in actions.Actions.Where(a => a.EngineReference == engine.Id && a.Status == "Blocked"))
            list.Add(new(engine.Id, "Blocked action", "repair", $"{action.Id}: {action.Title}"));
        if (list.Count == 0 && engine.Stages.Any(s => s.Value is "Waiting" or "Blocked"))
        {
            var stage = engine.Stages.First(s => s.Value is "Waiting" or "Blocked");
            list.Add(new(engine.Id, "Unexplained process wait", stage.Key,
                "The demonstration has a waiting/blocked status but no recorded cause. Investigate the source record."));
        }
        return list;
    }

    public static List<FleetBlockerGroup> Breakdown(IEnumerable<EngineCase> engines,
        ProcessSnapshot state, OperationsSnapshot actions)
    {
        var selected = engines.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        return engines.SelectMany(e => BlockersFor(e, state, actions))
            .GroupBy(b => new { b.Kind, b.ProcessId })
            .Select(g => new FleetBlockerGroup(g.Key.Kind, g.Key.ProcessId,
                g.Select(x => x.EngineId).Distinct().Count(),
                actions.Actions.Count(a => selected.Contains(a.EngineReference) && a.Status != "Done" &&
                    g.Any(b => b.EngineId == a.EngineReference)), g.First().Detail))
            .OrderByDescending(g => g.Visits).ToList();
    }

    public static int Pressure(ProcessDefinition process, IEnumerable<EngineCase> engines, ProcessMapSnapshot map,
        ProcessSnapshot state, string metric)
    {
        var rows = engines.Select(e => (Engine:e, Status:FleetAnalytics.Status(e, process, map)))
            .Where(x => x.Status != "Not applicable").ToList();
        if (rows.Count == 0) return 0;
        return metric switch
        {
            "holds" => Math.Clamp((int)Math.Round(100.0 * rows.Count(x => state.Parts.Any(p => p.EngineId == x.Engine.Id && p.Held))/rows.Count), 0, 100),
            "completion" => Math.Clamp(100 - (int)Math.Round(100.0 * rows.Count(x=>x.Status == "Complete")/rows.Count),0,100),
            "queue" => Math.Clamp((int)Math.Round(100.0 * rows.Count(x=> x.Status is "In progress" or "Ready" or "Waiting" or "Blocked") / rows.Count),0,100),
            _ => Math.Clamp((int)Math.Round(100.0 * rows.Count(x => x.Status is "Waiting" or "Blocked")/rows.Count),0,100)
        };
    }

    // Deliberately illustrative queue/capacity arithmetic, not an aircraft maintenance forecast.
    // No learned parameters, service intervals or real shop data are used.
    public static List<FleetForecastRow> Forecast(IEnumerable<ProcessDefinition> processes,
        IEnumerable<EngineCase> engines, ProcessMapSnapshot map,
        int workersPercent, int inboundPerDay, int materialAvailabilityPercent,
        int engineeringCapacityPercent)
    {
        var selected = engines.ToList();
        var stages = processes.ToList();
        var baseCap = Math.Max(1, selected.Count / 14.0);
        return stages.Select(p =>
        {
            var queue = selected.Count(e => FleetAnalytics.Status(e,p,map) is "In progress" or "Ready" or "Blocked" or "Waiting");
            var baseRate = baseCap * (p.ModuleId == "quality" ? .7 : p.ModuleId == "findings" ? .65 : 1.0);
            var scenarioFactor = workersPercent / 100.0;
            if (p.ModuleId is "parts" or "rebuild") scenarioFactor *= materialAvailabilityPercent / 100.0;
            if (p.ModuleId is "findings" or "quality") scenarioFactor *= engineeringCapacityPercent / 100.0;
            // Use floating-point division: otherwise small arrival rates truncate to zero
            // and Math.Round(int) is ambiguous between its decimal and double overloads.
            var arrivalShare = Math.Max(0, inboundPerDay) / (double)Math.Max(1, stages.Count);
            var baseline = queue / Math.Max(.25, baseRate);
            // New arrivals increase load over a fixed five-day horizon; projected queues
            // cannot be interpreted as validated cycle-time or release estimates.
            var simulatedQueue = queue + (int)Math.Round(arrivalShare * 5);
            var adjusted = simulatedQueue / Math.Max(.25, baseRate * scenarioFactor);
            return new FleetForecastRow(p.Id,p.Name,queue,Math.Round(baseRate*scenarioFactor,1),
                Math.Round(baseline,1),(int)Math.Round(arrivalShare*5),Math.Round(adjusted,1),
                baseline <= .01 ? 0 : (int)Math.Round(100*(adjusted / baseline - 1)));
        }).OrderByDescending(x=>x.ScenarioDays).ToList();
    }
}
