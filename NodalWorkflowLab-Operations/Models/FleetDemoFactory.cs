namespace NodalWorkflowLab.Models;

/// <summary>
/// Synthetic, deterministic demonstration records. No serial numbers, repair instructions,
/// inspection approvals or engine measurements here are real. The family-to-aircraft
/// descriptions are illustrative; all visit outcomes are entirely fabricated.
/// </summary>
public static class FleetDemoFactory
{
    public const int DefaultDemoFleetSize = 216;
    private static readonly (string Family, string Aircraft)[] Families =
    [
        ("PW1100G-JM", "Airbus A320neo family"),
        ("PW1500G", "Airbus A220 family"),
        ("PW1900G", "Embraer E-Jets E2 family"),
        ("V2500-A5", "Airbus A320 family"),
        ("PW4000-94", "Widebody service example"),
        ("PW2000", "Narrowbody service example")
    ];
    private static readonly string[] MainStages = ["induct", "teardown", "findings", "materials", "quality", "rebuild", "test", "release"];
    private static readonly string[] Locations =
    ["Receiving · Gate 1", "Induction Bay I-02", "Teardown Cell T-01", "Teardown Cell T-04",
     "Materials Staging M-05", "Engineering Review", "Quality Inspection Q-02",
     "Rebuild Cell R-03", "Test Cell TC-01", "Release Review"];
    private static readonly (string Description, string Module)[] PartTypes =
    [
        ("Fan module", "Fan"), ("Low-pressure compressor module", "LPC"),
        ("High-pressure compressor module", "HPC"), ("Combustor module", "Combustor"),
        ("High-pressure turbine module", "HPT"), ("Low-pressure turbine module", "LPT"),
        ("Accessory gearbox assembly", "Accessories"), ("Engine-mounted accessory", "Accessories"),
        ("Oil system component", "Oil system"), ("Bearing support assembly", "Structures")
    ];
    private static readonly string[] FictionalFindings =
    [
        "Synthetic inspection discrepancy requires engineering review.",
        "Training observation: incoming component documentation requires reconciliation.",
        "Illustrative part condition discrepancy pending authorized disposition.",
        "Demo-only quality observation; evidence package awaiting review.",
        "Synthetic hardware trace mismatch; component isolated for verification."
    ];
    private static readonly string[] SubstepIntake =
    ["intake-receive", "intake-identify", "intake-docs", "intake-scope", "intake-route"];

    public static int Expand(ProcessSnapshot snapshot, int targetCount = DefaultDemoFleetSize)
    {
        // Augment, never replace: a previous user's repair visits and events survive upgrades.
        var existing = snapshot.Engines.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var processMap = ProcessMapSnapshot.Seed();
        int added = 0;
        var start = DateTimeOffset.UtcNow;
        for (int i = 1; i <= targetCount; i++)
        {
            var id = $"FLEET-SIM-{i:0000}";
            if (!existing.Add(id)) continue;
            var (family, aircraft) = Families[(i * 7 + i / 9) % Families.Length];
            // Mix visit ages, statuses and special-case holds; there is no assumed universal repair route.
            var progress = (i * 13 + i / 3) % 10;
            var stageIndex = progress switch
            {
                0 => 0, 1 => 1, 2 => 1, 3 => 2, 4 => 3,
                5 => 4, 6 => 5, 7 => 5, 8 => 6, _ => 7
            };
            bool blocked = i % 9 == 0 || i % 17 == 0;
            bool waiting = !blocked && (i % 7 == 0 || i % 13 == 0);
            var currentIndex = stageIndex;
            string currentStatus = blocked ? "Blocked" : waiting ? "Waiting" : "In progress";
            if (stageIndex == 7 && i % 4 == 0) currentStatus = "Complete";
            var stages = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var stage = 0; stage < MainStages.Length; stage++)
                stages[MainStages[stage]] = stage < currentIndex ? "Complete" : stage == currentIndex ? currentStatus : "Not started";
            // Optional paths are not assumed to be complete just because an engine moves downstream.
            if (currentIndex > 2 && i % 4 == 0) stages["findings"] = "Not applicable";
            if (currentIndex > 4 && i % 5 == 0) stages["quality"] = "Not applicable";
            if (currentIndex > 3 && i % 6 == 0) stages["materials"] = "Complete";
            for (var s = 0; s < SubstepIntake.Length; s++)
                stages[SubstepIntake[s]] = currentIndex > 0 || (currentIndex == 0 && s < i % 5)
                    ? "Complete" : currentIndex == 0 && s == i % 5 ? "In progress" : "Not started";
            stages["intake-exception"] = i % 18 == 0 ? "Waiting" : "Not applicable";
            // Explicitly mark child process progress, rather than pretending to have real recorded operations.
            foreach (var parent in processMap.Processes.Where(p => p.ParentId is not null).Select(p => p.ParentId!).Distinct())
            {
                var kids = processMap.Children(parent).Where(c => c.Id != "intake-exception").ToList();
                if (kids.Count == 0 || parent == "induct") continue;
                var parentStage = FindAncestorStage(parent, processMap, stages);
                for (int j = 0; j < kids.Count; j++)
                {
                    var kid = kids[j];
                    var childStatus = parentStage switch
                    {
                        "Complete" => "Complete",
                        "Not applicable" => "Not applicable",
                        "Blocked" => j == Math.Min(kids.Count - 1, 1 + i % 2) ? "Blocked" : j < 1 ? "Complete" : "Not started",
                        "Waiting" => j == Math.Min(kids.Count - 1, 1 + i % 2) ? "Waiting" : j < 1 ? "Complete" : "Not started",
                        "In progress" or "Ready" => j < i % kids.Count ? "Complete" : j == i % kids.Count ? "In progress" : "Not started",
                        _ => "Not started"
                    };
                    if (kid.Id.StartsWith("intake-") || stages.ContainsKey(kid.Id)) continue;
                    stages[kid.Id] = childStatus;
                }
            }
            var engine = new EngineCase
            {
                Id = id, Family = family, Aircraft = aircraft,
                WorkOrder = $"WO-DEMO-{81000 + i:00000}",
                Cell = Locations[Math.Min(Locations.Length - 1, currentIndex + (i % 3 == 0 ? 1 : 0))],
                Narrative = "Synthetic demonstration visit. Operational history and results are invented.",
                Customer = $"Demo operator {1 + (i % 9):00}",
                Scope = (i % 4) switch { 0 => "Core module repair", 1 => "Performance restoration", 2 => "Limited work scope", _ => "Condition assessment" },
                ReceivedUtc = start.AddDays(-(2 + (i * 7) % 85)).AddHours(-(i % 17)),
                TargetUtc = start.AddDays(i % 11 == 0 ? -(1 + i % 9) : 3 + (i * 11) % 44),
                Stages = stages,
                StageProgress = stages.Where(s => s.Value is "In progress" or "Ready")
                    .ToDictionary(s => s.Key, s => 5 + (i * 17 + s.Key.Length * 13) % 68)
            };
            snapshot.Engines.Add(engine);
            added++;
            var parts = 5 + i % 7;
            for (int j = 0; j < parts; j++)
            {
                var kind = PartTypes[(j + i) % PartTypes.Length];
                bool held = (blocked || waiting) && j == i % parts;
                snapshot.Parts.Add(new TrackedPart
                {
                    Id = $"COMP-SIM-{i:0000}-{j + 1:00}", EngineId = id,
                    Description = kind.Description, Module = kind.Module,
                    Location = held ? "Quality hold rack Q-04" : j % 2 == 0 ? engine.Cell : "WIP rack W-03",
                    Held = held
                });
            }
            var findingCount = blocked ? 1 + i % 3 : waiting ? 1 + i % 2 : i % 4 == 0 ? 1 : 0;
            for (var f = 0; f < findingCount; f++)
            {
                var pending = f == 0 && (blocked || waiting);
                var findingId = $"FND-FLT-{i:0000}-{f + 1:00}";
                var runId = $"RUN-FLT-{i:0000}-{f + 1:00}";
                var partId = $"COMP-SIM-{i:0000}-{(i % parts) + 1:00}";
                var status = pending ? (i % 2 == 0 ? "Awaiting inspection" : "Pending disposition") : "Cleared (demo)";
                snapshot.Findings.Add(new ProcessFinding
                {
                    Id = findingId, EngineId = id, PartId = partId,
                    Description = FictionalFindings[(i + f) % FictionalFindings.Length],
                    ActionId = $"ACT-FLT-{i:0000}-{f + 1:00}", Status = status
                });
                if (pending)
                    snapshot.Runs.Add(new ProcessRun
                    {
                        Id = runId, EngineId = id, FindingId = findingId,
                        Status = "Waiting", WaitingFor = status == "Awaiting inspection"
                            ? "quality.inspection.recorded" : "engineering.disposition.recorded",
                        StartedUtc = engine.ReceivedUtc.AddDays(2 + f), UpdatedUtc = start.AddHours(-(i % 29))
                    });
                snapshot.Events.Add(Ev(id, "findings", pending ? "finding.waiting" : "finding.cleared.simulation",
                    $"{findingId} · {status} (entirely synthetic)", start.AddHours(-(2 + i % 100 + f)),
                    partId, $"ACT-FLT-{i:0000}-{f + 1:00}", pending ? runId : null));
            }
            var eventStages = Math.Min(8, currentIndex + 1);
            for (int stage = 0; stage < eventStages; stage++)
            {
                var stageId = MainStages[stage];
                var stageStatus = stages[stageId];
                if (stageStatus == "Not applicable") continue;
                snapshot.Events.Add(Ev(id, stageId == "materials" ? "parts" : stageId == "induct" ? "induction" : stageId,
                    stageStatus == "Complete" ? $"{stageId}.completed.demo" : $"{stageId}.started.demo",
                    $"Simulated {stageId} milestone for {engine.WorkOrder}.", engine.ReceivedUtc.AddHours(stage * (9 + i % 4))));
            }
            if (blocked) snapshot.Events.Add(Ev(id, "workflow", "process.blocked.demo", "Synthetic exception blocks downstream progress.", start.AddHours(-i % 48)));
        }
        return added;
    }

    private static string FindAncestorStage(string id, ProcessMapSnapshot map, Dictionary<string, string> stages)
    {
        var cursor = id;
        var visited = new HashSet<string>();
        while (visited.Add(cursor))
        {
            if (stages.TryGetValue(cursor, out var status)) return status;
            var parent = map.Processes.FirstOrDefault(p => p.Id == cursor)?.ParentId;
            if (string.IsNullOrEmpty(parent)) break;
            cursor = parent;
        }
        return "Not started";
    }

    private static ProcessAuditEvent Ev(string engineId, string module, string type, string detail,
        DateTimeOffset timestamp, string? partId = null, string? actionId = null, string? runId = null) => new()
    {
        Id = "EVT-FLT-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(), EngineId = engineId,
        TimestampUtc = timestamp, ModuleId = module, EventType = type, Detail = detail,
        PartId = partId, ActionId = actionId, RunId = runId, Actor = "synthetic-seed"
    };
}
