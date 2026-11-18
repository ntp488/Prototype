using Microsoft.AspNetCore.Hosting;
using System.Text.Json;
using NodalWorkflowLab.Models;

namespace NodalWorkflowLab.Services;

/// <summary>
/// File-backed, single-process demonstration of persisted waits and event-driven resumption.
/// NOT a regulated maintenance system, electronic signature system or production message broker.
/// </summary>
public sealed class ProcessOperationsStore
{
    private readonly string _path;
    private readonly OperationsStore _actions;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private ProcessSnapshot _state;
    private DateTimeOffset _lastAutoPulseUtc = DateTimeOffset.MinValue;

    public ProcessOperationsStore(IWebHostEnvironment environment, OperationsStore actions)
    {
        _actions = actions;
        _path = Path.Combine(environment.ContentRootPath, "App_Data", "process-scenarios.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _state = File.Exists(_path)
            ? JsonSerializer.Deserialize<ProcessSnapshot>(File.ReadAllText(_path))
              ?? throw new InvalidDataException("The scenario store could not be read.")
            : ProcessSnapshot.Seed();
        if (!File.Exists(_path)) File.WriteAllText(_path, JsonSerializer.Serialize(_state, Options));
    }

    /// <summary>Upgrade older fleet demo seeds that have a Quality wait but no action to work.</summary>
    public async Task<int> EnsurePendingInspectionActionsAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var created = 0;
            foreach (var finding in _state.Findings.Where(f => f.Status == "Awaiting inspection" && f.InspectionActionId is null))
            {
                var action = await _actions.CreateActionAsync("Inspect finding " + finding.Id,
                    "quality", "High", "Independent training-only inspection outcome required.", finding.EngineId);
                await _actions.AssignAsync(action.Id, "user", "u-sam", "Responsible");
                await _actions.AssignAsync(action.Id, "workspace", "quality", "Accountable");
                finding.InspectionActionId = action.Id;
                created++;
            }
            if (created > 0) await PersistAsync();
            return created;
        }
        finally { _gate.Release(); }
    }

    /// <summary>Idempotently adds a large synthetic demonstration fleet to existing data.</summary>
    public async Task<int> EnsureDemoFleetAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var added = FleetDemoFactory.Expand(_state);
            if (added > 0) await PersistAsync();
            // Operational action items are reconciled separately and have stable synthetic IDs.
            await _actions.EnsureFleetActionsAsync(_state);
            return added;
        }
        finally { _gate.Release(); }
    }

    /// <summary>
    /// One randomized, durable synthetic heartbeat. Partial progress never represents a
    /// physical operation, inspection decision, release or qualified sign-off.
    /// Waiting/blocked engines and engines with held parts cannot advance.
    /// </summary>
    public async Task<string> AdvanceFleetDemoAsync(int count = 0)
    {
        await _gate.WaitAsync();
        try
        {
            // Multiple open dashboards share this singleton. Never multiply auto progress
            // by the number of viewing browser tabs.
            var now = DateTimeOffset.UtcNow;
            if (count <= 0 && now - _lastAutoPulseUtc < TimeSpan.FromMilliseconds(900))
                return "Synthetic demo heartbeat already processed for this second.";
            if (count <= 0) _lastAutoPulseUtc = now;
            var keys = new[] { "induct", "teardown", "findings", "materials", "quality", "rebuild", "test", "release" };
            var candidates = _state.Engines
                .Where(e => e.Id.StartsWith("FLEET-SIM-", StringComparison.Ordinal))
                .Where(e => !_state.Parts.Any(p => p.EngineId == e.Id && p.Held))
                .Where(e => !_state.Runs.Any(r => r.EngineId == e.Id && r.Status == "Waiting"))
                .Where(e => !e.Stages.Values.Any(status => status is "Waiting" or "Blocked"))
                .Where(e => e.Stages.GetValueOrDefault("release") != "Complete")
                .ToList();
            if (candidates.Count == 0)
                return "No eligible synthetic visits. Human waits and component holds remain untouched.";

            var pulseCount = count <= 0 ? Random.Shared.Next(12, 30) : Math.Clamp(count, 1, 25);
            // Random selection rather than sweeping IDs in order; this is only demo telemetry.
            var selected = candidates.OrderBy(_ => Random.Shared.Next()).Take(pulseCount).ToList();
            var map = ProcessMapSnapshot.Seed();
            var advanced = 0;
            var completed = 0;
            foreach (var engine in selected)
            {
                var active = Array.FindIndex(keys, key => engine.Stages.GetValueOrDefault(key) is "In progress" or "Ready" or "Not started");
                if (active < 0) continue;
                var stage = keys[active];

                // These two major gates are always reserved for recorded human action.
                if (stage is "findings" or "quality" or "release")
                {
                    engine.Stages[stage] = "Waiting";
                    engine.StageProgress.Remove(stage);
                    Append(engine.Id, stage, "process.human.review.required.demo",
                        "Synthetic manual review required. No approval, inspection sign-off or return-to-service authorization was created.");
                    advanced++;
                    continue;
                }

                if (engine.Stages[stage] == "Not started") engine.Stages[stage] = "In progress";
                var previous = engine.StageProgress.GetValueOrDefault(stage);
                var delta = Random.Shared.Next(3, 18); // randomized 3-17 percentage points per tick
                var progress = Math.Min(100, previous + delta);
                engine.StageProgress[stage] = progress;
                advanced++;
                if (progress < 100)
                {
                    // Model some deeper work as independently visible partial completion.
                    AdvanceSyntheticSubsteps(engine, stage, progress, map);
                    continue;
                }

                engine.Stages[stage] = "Complete";
                engine.StageProgress[stage] = 100;
                CompleteSyntheticSubsteps(engine, stage, map);
                if (active + 1 < keys.Length)
                {
                    var next = keys[active + 1];
                    if (engine.Stages.GetValueOrDefault(next) == "Not started")
                        engine.Stages[next] = "In progress";
                }
                Append(engine.Id, stage == "induct" ? "induction" : stage, "process.stage.completed.demo",
                    $"Synthetic work progress reached 100%: {stage} for {engine.WorkOrder}.");
                await _actions.CompleteFleetStageActionAsync(engine.Id);
                completed++;
            }
            _state.DemoTick++;
            if (advanced > 0) await PersistAsync();
            return $"Advanced {advanced} synthetic visits; {completed} stage(s) completed. Holds and human gates remain enforced.";
        }
        finally { _gate.Release(); }
    }

    private static bool RequiresHumanEvidence(ProcessDefinition node) =>
        node.ModuleId == "quality" || node.Id == "intake-exception" ||
        node.Id.EndsWith("-wait", StringComparison.Ordinal) ||
        node.Id.EndsWith("-clear", StringComparison.Ordinal) ||
        node.Id.EndsWith("-inspect", StringComparison.Ordinal);

    private static void AdvanceSyntheticSubsteps(EngineCase engine, string parentId, int parentProgress, ProcessMapSnapshot map)
    {
        // Child workflow nodes reflect staged synthetic work without writing inspection evidence.
        var children = map.Children(parentId).Where(p => !RequiresHumanEvidence(p)).ToList();
        if (children.Count == 0) return;
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            var status = engine.Stages.GetValueOrDefault(child.Id);
            if (status is "Not applicable" or "Blocked" or "Waiting" or "Complete") continue;
            var part = Math.Clamp(parentProgress * children.Count - i * 100, 0, 100);
            if (part == 0) continue;
            engine.StageProgress[child.Id] = part;
            engine.Stages[child.Id] = part >= 100 ? "Complete" : "In progress";
            AdvanceSyntheticSubsteps(engine, child.Id, part, map);
        }
    }

    private static void CompleteSyntheticSubsteps(EngineCase engine, string parentId, ProcessMapSnapshot map)
    {
        foreach (var child in map.Children(parentId))
        {
            // Never manufacture an inspection approval, even in the demo.
            if (RequiresHumanEvidence(child)) continue;
            if (engine.Stages.GetValueOrDefault(child.Id) is "Not applicable" or "Blocked" or "Waiting") continue;
            engine.Stages[child.Id] = "Complete";
            engine.StageProgress[child.Id] = 100;
            CompleteSyntheticSubsteps(engine, child.Id, map);
        }
    }

    public async Task<ProcessSnapshot> SnapshotAsync()
    {
        await _gate.WaitAsync();
        try { return _state.Copy(); }
        finally { _gate.Release(); }
    }

    public async Task ResetAsync()
    {
        await _gate.WaitAsync();
        try { _state = ProcessSnapshot.Seed(); await PersistAsync(); }
        finally { _gate.Release(); }
    }

    /// <summary>Register an arbitrary fictional engine family into the universal plant intake process.</summary>
    public async Task<string> ReceiveEngineAsync(string family, string aircraft, string workOrder)
    {
        family = (family ?? "").Trim(); aircraft = (aircraft ?? "").Trim(); workOrder = (workOrder ?? "").Trim();
        if (family.Length is < 2 or > 65) throw new ArgumentException("Enter an engine family (2–65 characters).");
        if (aircraft.Length > 80 || workOrder.Length > 65) throw new ArgumentException("Aircraft or visit identifier is too long.");
        await _gate.WaitAsync();
        try
        {
            if (workOrder.Length != 0 && _state.Engines.Any(x => x.WorkOrder.Equals(workOrder, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("This demonstration repair-visit identifier is already registered.");
            var id = "ENG-SIM-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            if (workOrder.Length == 0) workOrder = "WO-" + id;
            var stageKeys = new[] {"induct", "teardown", "findings", "materials", "quality", "rebuild", "test", "release"};
            var stages = stageKeys.ToDictionary(k => k, _ => "Not started");
            stages["induct"] = "In progress";
            foreach (var step in new[] {"intake-receive", "intake-identify", "intake-docs", "intake-scope", "intake-route"})
                stages[step] = step == "intake-receive" ? "Complete" : step == "intake-identify" ? "Ready" : "Not started";
            stages["intake-exception"] = "Not applicable";
            var action = await _actions.CreateActionAsync("Complete Gate 1 intake for " + id,
                "induction", "High", "Synthetic intake · verify inbound identity, received records and work scope.", id);
            _state.Engines.Add(new EngineCase { Id = id, Family = family, Aircraft = aircraft.Length == 0 ? "Aircraft association not provided" : aircraft,
                WorkOrder = workOrder, IntakeActionId = action.Id, Cell = "Gate 1 · Receiving", Narrative = "New synthetic repair visit. Universal intake in progress.", Stages = stages,
                StageProgress = new Dictionary<string, int> { ["induct"] = 5, ["intake-identify"] = 0 } });
            Append(id, "induction", "engine.received", "Universal Gate 1 intake opened for " + family + "; work order " + workOrder + ".",
                actionId: action.Id);
            await PersistAsync();
            return id;
        }
        finally { _gate.Release(); }
    }

    /// <summary>Simulates progression through five generic intake tasks; not a real inspection or release gate.</summary>
    public async Task<string> AdvanceIntakeAsync(string engineId)
    {
        await _gate.WaitAsync();
        try
        {
            var engine = Engine(engineId);
            if (engine.Stages.GetValueOrDefault("induct") == "Complete") return "Gate 1 is already complete for this visit.";
            string[] steps = ["intake-receive", "intake-identify", "intake-docs", "intake-scope", "intake-route"];
            var next = steps.FirstOrDefault(x => engine.Stages.GetValueOrDefault(x) != "Complete");
            if (next is null) return "All Gate 1 tasks have already been recorded.";
            engine.Stages[next] = "Complete";
            var following = steps.SkipWhile(x => x != next).Skip(1).FirstOrDefault();
            if (following is not null) engine.Stages[following] = "Ready";
            Append(engineId, "induction", "intake.step.completed", $"Demonstration Gate 1 task completed: {next}.", actionId: engine.IntakeActionId);
            if (following is null)
            {
                engine.Stages["induct"] = "Complete";
                engine.Stages["teardown"] = "Ready";
                engine.Cell = "Teardown planning queue";
                if (engine.IntakeActionId is not null) await _actions.SetVerifiedStatusAsync(engine.IntakeActionId, "Done");
                Append(engineId, "induction", "intake.gate1.completed", "Generic intake completed; visit eligible for next process in this simulation.", actionId: engine.IntakeActionId);
            }
            else engine.Stages["induct"] = "In progress";
            await PersistAsync();
            return following is null ? "Gate 1 intake completed for this simulated repair visit." : $"Recorded {next}; next task is {following}.";
        }
        finally { _gate.Release(); }
    }

    public async Task<string> ReportFindingAsync(string engineId, string partId, string description)
    {
        if (string.IsNullOrWhiteSpace(description) || description.Length > 500)
            throw new ArgumentException("Describe the observation in 1–500 characters.");
        await _gate.WaitAsync();
        try
        {
            var engine = Engine(engineId);
            var part = Part(partId, engineId);
            var action = await _actions.CreateActionAsync(
                "Evaluate finding on " + part.Description, "engineering", "High", description, engineId);
            await _actions.AssignAsync(action.Id, "user", "u-jordan", "Responsible");
            await _actions.AssignAsync(action.Id, "workspace", "engineering", "Accountable");
            var id = "FND-SIM-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            var runId = "RUN-" + Guid.NewGuid().ToString("N")[..9].ToUpperInvariant();
            var finding = new ProcessFinding { Id=id, EngineId=engineId, PartId=partId,
                Description=description.Trim(), ActionId=action.Id };
            var run = new ProcessRun { Id=runId, EngineId=engineId, FindingId=id,
                WaitingFor="engineering.disposition.recorded", Status="Waiting" };
            _state.Findings.Add(finding);
            _state.Runs.Add(run);
            part.Held = true;
            engine.Stages["findings"] = "Waiting";
            engine.Stages["materials"] = "Blocked";
            engine.Stages["rebuild"] = "Blocked";
            Append(engineId, "findings", "finding.reported", $"{id} · {description.Trim()}", partId, action.Id, runId);
            Append(engineId, "parts", "part.held", $"Automatic simulated hold applied to {partId}.", partId, action.Id, runId);
            Append(engineId, "workflow", "workflow.waiting", $"Waiting for {run.WaitingFor}.", partId, action.Id, runId);
            await PersistAsync();
            return id;
        }
        finally { _gate.Release(); }
    }

    /// <summary>Trace a manually created general action when it references a known demo visit.</summary>
    public async Task<bool> RecordGeneralActionEventAsync(string engineId, string actionId, string title)
    {
        await _gate.WaitAsync();
        try
        {
            if (!_state.Engines.Any(e=>e.Id==engineId)) return false;
            Append(engineId,"actions","action.created.demo",$"General coordination task created: {title}.",actionId:actionId);
            await PersistAsync();
            return true;
        }
        finally { _gate.Release(); }
    }

    public async Task<string> MovePartAsync(string engineId, string partId, string destination)
    {
        if (string.IsNullOrWhiteSpace(destination) || destination.Length > 80)
            throw new ArgumentException("Choose a valid destination.");
        await _gate.WaitAsync();
        try
        {
            _ = Engine(engineId);
            var part = Part(partId, engineId);
            if (part.Held)
            {
                Append(engineId, "parts", "part.movement.rejected",
                    $"Movement to {destination} rejected: {partId} has an active simulated hold.", partId);
                await PersistAsync();
                return "Movement rejected — component is on hold. Rejection was recorded in the audit timeline.";
            }
            var previous = part.Location;
            part.Location = destination;
            Append(engineId, "parts", "part.moved", $"{partId}: {previous} → {destination}.", partId);
            await PersistAsync();
            return $"Movement recorded: {partId} → {destination}.";
        }
        finally { _gate.Release(); }
    }

    public async Task<string> RecordDispositionAsync(string findingId, bool accepted)
    {
        await _gate.WaitAsync();
        try
        {
            var finding = Finding(findingId);
            if (finding.Status != "Pending disposition")
                throw new InvalidOperationException("This finding is not waiting for engineering disposition.");
            var run = Run(finding);
            if (run.WaitingFor != "engineering.disposition.recorded")
                throw new InvalidOperationException("The process execution is not subscribed to this event.");
            var engine = Engine(finding.EngineId);
            finding.DispositionBy = "SIMULATED ENGINEERING REVIEW";
            Append(engine.Id, "findings", "engineering.disposition.recorded",
                accepted ? "Demonstration decision: accepted for inspection routing." : "Demonstration decision: revision requested.",
                finding.PartId, finding.ActionId, run.Id);
            if (!accepted)
            {
                await _actions.SetVerifiedStatusAsync(finding.ActionId, "Blocked");
                finding.Status = "Disposition revision requested";
                run.Status = "Waiting";
                run.WaitingFor = "engineering.disposition.revised";
                Append(engine.Id, "workflow", "workflow.waiting", "Revision required; simulated hold remains active.",
                    finding.PartId, finding.ActionId, run.Id);
                run.UpdatedUtc = DateTimeOffset.UtcNow;
                await PersistAsync();
                return "Revision requested. The affected component remains on hold.";
            }
            // A disposition does NOT sign off an inspection and does NOT clear a component hold.
            var inspectionAction = await _actions.CreateActionAsync(
                "Independent inspection for " + finding.Id, "quality", "High",
                "Simulated training request only. No regulatory signature is performed.", engine.Id);
            finding.InspectionActionId = inspectionAction.Id;
            await _actions.AssignAsync(inspectionAction.Id, "user", "u-sam", "Responsible");
            await _actions.AssignAsync(inspectionAction.Id, "workspace", "quality", "Accountable");
            await _actions.SetVerifiedStatusAsync(finding.ActionId, "Done");
            finding.Status = "Awaiting inspection";
            engine.Stages["quality"] = "Waiting";
            run.WaitingFor = "quality.inspection.recorded";
            run.Status = "Waiting";
            run.UpdatedUtc = DateTimeOffset.UtcNow;
            Append(engine.Id, "quality", "inspection.requested",
                $"Inspection action {inspectionAction.Id} opened; independent sign-off pending.",
                finding.PartId, inspectionAction.Id, run.Id);
            Append(engine.Id, "workflow", "workflow.waiting", "Waiting for quality.inspection.recorded.",
                finding.PartId, finding.ActionId, run.Id);
            await PersistAsync();
            return "Disposition simulated. Inspection action created. Workflow still waiting; part remains held.";
        }
        finally { _gate.Release(); }
    }

    public async Task<string> ResubmitDispositionAsync(string findingId)
    {
        await _gate.WaitAsync();
        try
        {
            var finding = Finding(findingId);
            var run = Run(finding);
            if (finding.Status != "Disposition revision requested" || run.WaitingFor != "engineering.disposition.revised")
                throw new InvalidOperationException("This finding is not waiting for a revised disposition.");
            finding.Status = "Pending disposition";
            await _actions.SetVerifiedStatusAsync(finding.ActionId, "In Progress");
            run.WaitingFor = "engineering.disposition.recorded";
            run.UpdatedUtc = DateTimeOffset.UtcNow;
            Append(finding.EngineId, "findings", "engineering.disposition.revised",
                "A synthetic revised disposition was submitted for another decision.",
                finding.PartId, finding.ActionId, run.Id);
            await PersistAsync();
            return "Revised disposition submitted. The workflow is waiting for another engineering decision.";
        }
        finally { _gate.Release(); }
    }

    public async Task<string> RecordInspectionAsync(string findingId, bool accepted)
    {
        await _gate.WaitAsync();
        try
        {
            var finding = Finding(findingId);
            if (finding.Status != "Awaiting inspection")
                throw new InvalidOperationException("An accepted disposition is required before inspection recording.");
            var run = Run(finding);
            if (run.WaitingFor != "quality.inspection.recorded")
                throw new InvalidOperationException("The process execution is waiting for another event.");
            var engine = Engine(finding.EngineId);
            // Older synthetic fleet seeds can enter the inspection wait without a matching
            // action record. Create its typed downstream task before recording the outcome.
            if (finding.InspectionActionId is null)
            {
                var inspectionTask = await _actions.CreateActionAsync(
                    "Independent inspection for " + finding.Id, "quality", "High",
                    "Synthetic inspection: training-only, not a maintenance release.", engine.Id);
                finding.InspectionActionId = inspectionTask.Id;
                await _actions.AssignAsync(inspectionTask.Id, "user", "u-sam", "Responsible");
                await _actions.AssignAsync(inspectionTask.Id, "workspace", "quality", "Accountable");
            }
            finding.InspectionBy = "SIMULATED QUALITY REVIEW";
            Append(engine.Id, "quality", "quality.inspection.recorded",
                accepted ? "Demonstration inspection accepted (not a regulatory sign-off)."
                         : "Demonstration inspection rejected; additional engineering work required.",
                finding.PartId, finding.ActionId, run.Id);
            if (!accepted)
            {
                if (finding.InspectionActionId is not null)
                    await _actions.SetVerifiedStatusAsync(finding.InspectionActionId, "Blocked");
                await _actions.SetVerifiedStatusAsync(finding.ActionId, "Blocked");
                finding.Status = "Pending disposition";
                run.WaitingFor = "engineering.disposition.recorded";
                run.Status = "Waiting";
                run.UpdatedUtc = DateTimeOffset.UtcNow;
                engine.Stages["quality"] = "Blocked";
                Append(engine.Id, "workflow", "workflow.waiting", "Returned to engineering following failed inspection.",
                    finding.PartId, finding.ActionId, run.Id);
                await PersistAsync();
                return "Inspection rejected in simulation; workflow returned to engineering. Hold maintained.";
            }
            if (finding.InspectionActionId is not null)
                await _actions.SetVerifiedStatusAsync(finding.InspectionActionId, "Done");
            // The seed may have retained a blocked Engineering action while a Quality wait
            // was already open; the verified quality event reconciles both actions.
            await _actions.SetVerifiedStatusAsync(finding.ActionId, "Done");
            finding.Status = "Cleared (demo)";
            run.Status = "Completed";
            run.WaitingFor = "";
            run.UpdatedUtc = DateTimeOffset.UtcNow;
            var part = Part(finding.PartId, engine.Id);
            part.Held = _state.Findings.Any(f => f.PartId == part.Id && f.Status != "Cleared (demo)");
            var open = _state.Findings.Any(f => f.EngineId == engine.Id && f.Status != "Cleared (demo)");
            engine.Stages["quality"] = open ? "Waiting" : "Complete";
            engine.Stages["findings"] = open ? "Waiting" : "Complete";
            engine.Stages["materials"] = _state.Parts.Any(p => p.EngineId == engine.Id && p.Held) ? "Blocked" : "Complete";
            if (!open) engine.Stages["rebuild"] = "Ready";
            Append(engine.Id, "parts", part.Held ? "part.hold.retained" : "part.hold.cleared",
                part.Held ? "Another finding still holds this part." : "Simulated hold cleared after inspection evidence.",
                part.Id, finding.ActionId, run.Id);
            Append(engine.Id, "workflow", "workflow.completed", $"{run.Id} resumed and completed.",
                part.Id, finding.ActionId, run.Id);
            await PersistAsync();
            return "Simulated inspection recorded. Waiting execution resumed and readiness was recalculated.";
        }
        finally { _gate.Release(); }
    }

    public async Task<string> AdvanceRebuildAsync(string engineId)
    {
        await _gate.WaitAsync();
        try
        {
            var engine = Engine(engineId);
            if (engine.Stages.GetValueOrDefault("induct") != "Complete")
            {
                Append(engineId, "rebuild", "rebuild.start.rejected", "Site Gate 1 has not completed.");
                await PersistAsync();
                return "Rebuild blocked: Gate 1 plant intake is not complete.";
            }
            if (_state.Parts.Any(p => p.EngineId == engineId && p.Held) ||
                _state.Findings.Any(f => f.EngineId == engineId && f.Status != "Cleared (demo)"))
            {
                Append(engineId, "rebuild", "rebuild.start.rejected", "Readiness rejected: unresolved findings or component holds.");
                await PersistAsync();
                return "Rebuild blocked: outstanding findings or held parts.";
            }
            var current = engine.Stages.GetValueOrDefault("rebuild", "Not started");
            if (current == "Complete") return "Rebuild is already complete in this demonstration.";
            if (current == "In progress")
            {
                engine.Stages["rebuild"] = "Complete";
                engine.Stages["test"] = "Ready";
                Append(engineId, "rebuild", "rebuild.completed", "Synthetic build evidence marked complete; test can be scheduled.");
                await PersistAsync();
                return "Simulated rebuild completed. Test is ready to begin.";
            }
            if (current is not ("Ready" or "Blocked"))
                return "Rebuild is not at a startable stage in this sample scenario.";
            engine.Stages["rebuild"] = "In progress";
            Append(engineId, "rebuild", "rebuild.started", "Simulated readiness checks passed; rebuild started.");
            await PersistAsync();
            return "Simulated rebuild started. Run again to mark the demonstration rebuild complete.";
        }
        finally { _gate.Release(); }
    }

    public async Task<string> RecordTestAsync(string engineId, bool pass)
    {
        await _gate.WaitAsync();
        try
        {
            var engine = Engine(engineId);
            if (engine.Stages.GetValueOrDefault("rebuild") != "Complete")
                return "Test cannot be recorded until the simulated rebuild is complete.";
            if (engine.Stages.GetValueOrDefault("test") == "Complete")
                return "The sample test has already been recorded.";
            engine.Stages["test"] = pass ? "Complete" : "Blocked";
            engine.Stages["release"] = pass ? "Review required" : "Blocked";
            Append(engineId, "test", pass ? "test.passed" : "test.failed",
                pass ? "Illustrative test-result entry: pass. Separate release approval still required."
                     : "Illustrative test-result entry: fail. Investigation required.");
            if (!pass)
            {
                var item = await _actions.CreateActionAsync(
                    "Investigate simulated engine test result", "engineering", "High",
                    "Training-only test exception; no real measured parameters.", engineId);
                Append(engineId, "test", "test.investigation.created", $"Action {item.Id} assigned to Repair Engineering.", actionId:item.Id);
            }
            await PersistAsync();
            return pass ? "Test passed in simulation. Release remains a separate human-controlled activity."
                        : "Test failed in simulation. Investigation action created and release blocked.";
        }
        finally { _gate.Release(); }
    }

    private EngineCase Engine(string id) => _state.Engines.FirstOrDefault(x => x.Id == id)
        ?? throw new ArgumentException("Engine not found.");
    private TrackedPart Part(string id, string engineId) => _state.Parts.FirstOrDefault(x => x.Id == id && x.EngineId == engineId)
        ?? throw new ArgumentException("Part not found for selected engine.");
    private ProcessFinding Finding(string id) => _state.Findings.FirstOrDefault(x => x.Id == id)
        ?? throw new ArgumentException("Finding not found.");
    private ProcessRun Run(ProcessFinding finding) => _state.Runs.First(x => x.FindingId == finding.Id);
    private void Append(string engine, string module, string type, string detail,
        string? partId = null, string? actionId = null, string? runId = null)
    {
        _state.Events.Add(new ProcessAuditEvent
        {
            Id="EVT-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            EngineId=engine, PartId=partId, ActionId=actionId, RunId=runId,
            ModuleId=module, EventType=type, Detail=detail, Actor="SIMULATED ACTOR / SYSTEM"
        });
    }
    private async Task PersistAsync()
    {
        var temp = _path + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(_state, Options));
        File.Move(temp, _path, overwrite:true);
    }
}
