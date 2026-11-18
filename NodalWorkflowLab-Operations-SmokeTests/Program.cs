using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using NodalWorkflowLab.Models;
using NodalWorkflowLab.Services;

static void Verify(bool ok, string message)
{
    if (!ok) throw new Exception("FAIL: " + message);
    Console.WriteLine("PASS: " + message);
}

var root = Path.Combine(Path.GetTempPath(), "nodal-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var host = new TestEnvironment(root);
    var library = new WorkflowLibraryStore(host);
    var operations = new OperationsStore(host);
    var processes = new ProcessOperationsStore(host, operations);
    var runner = new WorkflowRunner(operations, library, processes);
    var workflows = await library.ListAsync();
    Verify(workflows.Count >= 7, "Seven or more seeded workflows exist, including reusable compositions");
    Verify(workflows.All(w => w.Graph.Validate(out _)), "All seeded workflow graphs are valid");

    var processMap = new ProcessMapStore(host, library);
    var mapBefore = await processMap.SnapshotAsync();
    Verify(mapBefore.Children("repair").Count == 8, "Editable site process starts with eight module processes");
    var createdProcess = await processMap.AddChildAsync("repair", "quality",
        "Synthetic evidence review", "Reusable definition smoke test", "quality");
    var mapAfter = await processMap.SnapshotAsync();
    Verify(mapAfter.Children("repair").Count == 9, "Author can add a child process to a process group");
    Verify(mapAfter.Links.Any(l => l.FromId == "quality" && l.ToId == createdProcess.Id),
        "New process is linked to its selected predecessor");
    Verify(await library.GetAsync(createdProcess.WorkflowId!) is not null,
        "New process receives a saved editable starter workflow");
    var mapReopened = new ProcessMapStore(host, library);
    Verify((await mapReopened.SnapshotAsync()).Processes.Any(p => p.Id == createdProcess.Id),
        "User-authored process hierarchy survives repository restart");
    var preventedCycle = false;
    try { await processMap.AddLinkAsync("repair", createdProcess.Id, "quality"); }
    catch (InvalidOperationException) { preventedCycle = true; }
    Verify(preventedCycle, "Process dependency cycle is rejected");
    Verify(ProcessCatalog.Children("repair").Count == 8, "Root process map contains eight module processes");
    Verify(mapBefore.Children("induct").Count == 6, "Universal Gate 1 includes receipt, identity, records, scope, routing and exception path");
    Verify(mapBefore.Get("induct").Applicability == "All engines", "Gate 1 is not restricted to a specific engine family");
    Verify(mapBefore.Get("induct").WorkflowId == "gate1-intake-graph", "Gate 1 references its own reusable workflow");
    var extraUniversalStep = await processMap.AddChildAsync("induct", "induction", "Review receiving evidence",
        "Synthetic child of the universal intake gate", "intake-docs");
    Verify(extraUniversalStep.Applicability == "All engines", "Nested Gate 1 steps inherit universal applicability");
    Verify(ProcessCatalog.Links.All(l => ProcessCatalog.Processes.Any(p => p.Id == l.ParentId)
        && ProcessCatalog.Processes.Any(p => p.Id == l.FromId && p.ParentId == l.ParentId)
        && ProcessCatalog.Processes.Any(p => p.Id == l.ToId && p.ParentId == l.ParentId)),
        "Every process-map connection stays inside its parent process definition");
    Verify(ProcessCatalog.Processes.Where(p => p.WorkflowId is not null)
        .All(p => workflows.Any(w => w.Id == p.WorkflowId)), "Every linked process has a stored node workflow");
    var site = workflows.Single(w => w.Id == "end-to-end-repair");
    var compositeMessages = new List<WorkflowEvent>();
    await runner.RunAsync(site.Graph, "site-start", new WorkflowInput("PW-DEMO-1007"),
        evt => { compositeMessages.Add(evt); return Task.CompletedTask; });
    Verify(compositeMessages.Count(e => e.Text?.Contains("Entering reusable workflow") == true) == 4,
        "Root graph invokes Gate 1 and three reusable downstream child workflows");

    var initialScenario = await processes.SnapshotAsync();
    Verify(initialScenario.Engines.Count == 3, "Three synthetic engine cases use public GTF product families");
    var initialFinding = initialScenario.Findings.Single(f => f.Id == "FND-SIM-001");
    var heldPart = initialScenario.Parts.Single(p => p.Id == initialFinding.PartId);
    Verify(heldPart.Held && initialScenario.Runs.Any(r => r.FindingId == initialFinding.Id && r.Status == "Waiting"),
        "Seeded finding has a held component and persistent waiting execution");
    var denied = await processes.MovePartAsync(initialFinding.EngineId, heldPart.Id, "WIP Rack W-03");
    Verify(denied.Contains("rejected", StringComparison.OrdinalIgnoreCase), "Held-part barcode movement is rejected");
    var afterDenied = await processes.SnapshotAsync();
    Verify(afterDenied.Parts.Single(p => p.Id == heldPart.Id).Location == heldPart.Location,
        "Rejected movement does not alter recorded location");
    var reloadedProcesses = new ProcessOperationsStore(host, operations);
    Verify((await reloadedProcesses.SnapshotAsync()).Events.Any(e => e.EventType == "part.movement.rejected"),
        "Denied move audit event survives repository reinitialization");
    await processes.RecordDispositionAsync(initialFinding.Id, accepted: true);
    var afterDisposition = await processes.SnapshotAsync();
    Verify(afterDisposition.Parts.Single(p => p.Id == heldPart.Id).Held,
        "Engineering disposition does not bypass a separate inspection hold");
    Verify(afterDisposition.Runs.Single(r => r.FindingId == initialFinding.Id).WaitingFor == "quality.inspection.recorded",
        "Durable execution advances to independent inspection wait");
    var inspectionActionId = afterDisposition.Findings.Single(f => f.Id == initialFinding.Id).InspectionActionId;
    Verify(inspectionActionId is not null, "Disposition generates a linked Quality action item");
    Verify((await operations.SnapshotAsync()).Actions.Single(a => a.Id == initialFinding.ActionId).Status == "Done",
        "Engineering disposition updates the linked action item");
    await processes.RecordInspectionAsync(initialFinding.Id, accepted: true);
    var afterInspection = await processes.SnapshotAsync();
    Verify(!afterInspection.Parts.Single(p => p.Id == heldPart.Id).Held, "Simulated clearance releases the held component");
    Verify(afterInspection.Runs.Single(r => r.FindingId == initialFinding.Id).Status == "Completed",
        "Inspection event resumes and completes persisted waiting process");
    Verify((await operations.SnapshotAsync()).Actions.Single(a => a.Id == inspectionActionId).Status == "Done",
        "Recorded demonstration inspection completes its linked Quality action");
    var moved = await processes.MovePartAsync(initialFinding.EngineId, heldPart.Id, "WIP Rack W-03");
    Verify(moved.StartsWith("Movement recorded"), "Cleared component may be moved in the simulation");
    await processes.AdvanceRebuildAsync(initialFinding.EngineId);
    await processes.AdvanceRebuildAsync(initialFinding.EngineId);
    var testResult = await processes.RecordTestAsync(initialFinding.EngineId, pass: true);
    Verify(testResult.Contains("Release remains"), "Passing test never performs release authorization");

    var data = await operations.SnapshotAsync();
    Verify(data.Workspaces.Count >= 9, "Hierarchy has parent and child workspaces");
    Verify(OperationsRollups.DescendantIds(data, "cec").Count == data.Workspaces.Count,
        "Parent workspace recursively reaches all descendants");
    Verify(OperationsRollups.Summarize(data, "cec").Total == data.Actions.Count,
        "Root dashboard deduplicates actions counted through multiple workspace associations");

    var blocked = workflows.Single(w => w.Id == "blocked-escalation");
    var item = data.Actions.Single(a => a.Id == "ACT-1001");
    await operations.SetStatusAsync(item.Id, "Blocked");
    var messages = new List<WorkflowEvent>();
    await runner.RunAsync(blocked.Graph, "blocked-event",
        new WorkflowInput("Blocked", item.Id, "status-changed"),
        evt => { messages.Add(evt); return Task.CompletedTask; });
    data = await operations.SnapshotAsync();
    var routed = data.Actions.Single(a => a.Id == item.Id);
    Verify(routed.AssignedWorkspaceId == "engineering", "Blocked item is routed to Repair Engineering");
    Verify(routed.Assignments.Any(r => r.Role == "Responsible" && r.PrincipalId == "u-jordan"),
        "Escalation applies Responsible RACI assignment");
    Verify(messages.Any(e => e.Type == "run-complete"), "Execution emits completion events");

    var quality = workflows.Single(w => w.Id == "quality-followup");
    var before = data.Actions.Count;
    await operations.SetStatusAsync("ACT-1006", "Done");
    await runner.RunAsync(quality.Graph, "quality-event",
        new WorkflowInput("Done", "ACT-1006", "status-changed"),
        _ => Task.CompletedTask);
    data = await operations.SnapshotAsync();
    Verify(data.Actions.Count == before + 1, "Completion creates one follow-up action");
    var review = data.Actions.Last();
    Verify(review.OwnerWorkspaceId == "quality" && review.Title.Contains("Approve assembly readiness"),
        "Follow-up is owned by Quality and resolves action-title token");

    var copy = workflows[0].Copy();
    copy.Id = "saved-test";
    copy.Name = "Persisted smoke workflow";
    await library.SaveAsync(copy);
    Verify((await library.GetAsync(copy.Id))?.Name == copy.Name, "Workflow saves and reloads by ID");
    var reopened = new WorkflowLibraryStore(host);
    Verify((await reopened.GetAsync(copy.Id))?.Name == copy.Name, "Saved workflow survives repository restart");
    await library.DeleteAsync(copy.Id);
    Verify(await library.GetAsync(copy.Id) is null, "Workflow deletion is persisted");

    // Universal Gate 1 applies to a brand-new, arbitrary engine family and repair visit.
    var newEngineId = await processes.ReceiveEngineAsync("V2500-A5", "Airbus A320 family", "WO-SIM-NEW-001");
    var newEngine = (await processes.SnapshotAsync()).Engines.Single(e => e.Id == newEngineId);
    Verify(newEngine.Family == "V2500-A5" && newEngine.Stages["induct"] == "In progress",
        "Any fictional engine family enters the same Gate 1 process");
    Verify((await operations.SnapshotAsync()).Actions.Any(a => a.EngineReference == newEngineId && a.OwnerWorkspaceId == "induction"),
        "Receipt creates a linked intake action item in the Induction workspace");
    var gateDenied = await processes.AdvanceRebuildAsync(newEngineId);
    Verify(gateDenied.Contains("Gate 1"), "Downstream rebuild cannot bypass uncompleted plant intake");
    for (var i = 0; i < 4; i++) await processes.AdvanceIntakeAsync(newEngineId);
    newEngine = (await processes.SnapshotAsync()).Engines.Single(e => e.Id == newEngineId);
    Verify(newEngine.Stages["induct"] == "Complete" && newEngine.Stages["teardown"] == "Ready",
        "Generic Gate 1 steps complete and route to teardown readiness");
    Verify((await operations.SnapshotAsync()).Actions.Single(a => a.EngineReference == newEngineId && a.Id == newEngine.IntakeActionId).Status == "Done",
        "Intake completion resolves its own organizational action item");
    var restarted = new ProcessOperationsStore(host, operations);
    Verify((await restarted.SnapshotAsync()).Engines.Single(e => e.Id == newEngineId).Stages["induct"] == "Complete",
        "New engine visit and intake progression survive repository restart");

    // Rich fleet dashboard: idempotent synthetic data, deep process topology,
    // per-engine process aggregation, and safeguards around blocked parts.
    var seedCount = await processes.EnsureDemoFleetAsync();
    Verify(seedCount == FleetDemoFactory.DefaultDemoFleetSize, "First demo expansion seeds 216 additional fictional repair visits");
    Verify(await processes.EnsureDemoFleetAsync() == 0, "Reopening the application does not duplicate synthetic visits");
    var fleet = await processes.SnapshotAsync();
    var fleetActions = await operations.SnapshotAsync();
    Verify(fleet.Engines.Count == 4 + FleetDemoFactory.DefaultDemoFleetSize,
        "Demo fleet augments instead of replacing three starter visits and user-created intake");
    Verify(fleet.Engines.Select(e => e.Family).Distinct().Count() >= 5,
        "Demo fleet represents several illustrative engine families");
    Verify(fleet.Parts.Count > 1000 && fleet.Events.Count > 500 && fleet.Findings.Count > 40,
        "Dense parts, event history and exception records populate the dashboard");
    Verify(fleetActions.Actions.Count(a => a.EngineReference.StartsWith("FLEET-SIM-", StringComparison.Ordinal)) > 200,
        "Synthetic action items tie individual visits to workspace rollups");
    Verify(mapBefore.Children("teardown").Count >= 4 && mapBefore.Children("td-disassemble").Count >= 4
        && mapBefore.Children("td-compressor").Count >= 2 && mapBefore.Children("rb-core").Count >= 2,
        "Drillable process map includes at least four levels in teardown and rebuild");
    var scope = mapBefore.Get("teardown");
    var metrics = FleetAnalytics.Summarize(scope, fleet.Engines, mapBefore, fleet, fleetActions);
    Verify(metrics.Total <= fleet.Engines.Count && metrics.Total > 50 && metrics.Complete > 0,
        "Fleet process metrics count distinct engine visits without duplicate rollups");
    Verify(FleetVisitSlicer.MatchesStatus("In progress", "", "current", false)
           && FleetVisitSlicer.MatchesStatus("Waiting", "", "current", false)
           && FleetVisitSlicer.MatchesStatus("Ready", "", "current", false)
           && !FleetVisitSlicer.MatchesStatus("Complete", "", "current", false)
           && !FleetVisitSlicer.MatchesStatus("Untracked", "", "current", false),
        "Node selection isolates current work and does not confuse prior completion with current location");
    Verify(FleetVisitSlicer.MatchesStatus("Complete", "Waiting", "current", true)
           && !FleetVisitSlicer.MatchesStatus("Complete", "Complete", "current", true)
           && !FleetVisitSlicer.MatchesStatus("In progress", "Ready", "current", true),
        "Edge selection shows current source-complete to destination-active handoffs");
    Verify(FleetVisitSlicer.MatchesStatus("Complete", "", "all", false)
           && FleetVisitSlicer.MatchesStatus("Complete", "", "Complete", false),
        "The eligible-process view is explicitly available without polluting the current-step slice");
    var firstLink = mapBefore.Links.First(l => l.ParentId == "repair");
    var handoff = FleetAnalytics.SummarizeLink(mapBefore.Get(firstLink.FromId), mapBefore.Get(firstLink.ToId), fleet.Engines, mapBefore);
    Verify(handoff.Total > 50 && handoff.Transitioned <= handoff.SourceComplete,
        "Process connection aggregates only eligible visit transitions");
    var heldDemo = fleet.Parts.First(p => p.EngineId.StartsWith("FLEET-SIM-") && p.Held);
    var waitsBeforePulse = fleet.Runs.Count(r => r.Status == "Waiting" && r.EngineId.StartsWith("FLEET-SIM-"));
    var demoMoved = await processes.MovePartAsync(heldDemo.EngineId, heldDemo.Id, "Rebuild Cell R-01");
    Verify(demoMoved.Contains("rejected", StringComparison.OrdinalIgnoreCase),
        "Fleet simulation respects individual component holds");
    var pulseResult = await processes.AdvanceFleetDemoAsync(6);
    var pulseSnapshot = await processes.SnapshotAsync();
    Verify(pulseResult.Contains("synthetic") && pulseSnapshot.DemoTick > 0,
        "Demo live pulse advances eligible synthetic process milestones");
    Verify(pulseSnapshot.Engines.Any(e => e.Id.StartsWith("FLEET-SIM-", StringComparison.Ordinal) &&
        e.StageProgress.Values.Any(p => p is > 0 and < 100)),
        "Per-engine partial progress values are stored independently of completed milestones");
    Verify(pulseSnapshot.Engines.Any(e => e.Id.StartsWith("FLEET-SIM-", StringComparison.Ordinal) &&
        e.Stages.Values.Any(v => v == "Waiting")),
        "Human-review gates remain waiting, not signed by synthetic progress");
    Verify(pulseSnapshot.Parts.Single(p => p.Id == heldDemo.Id).Held &&
        pulseSnapshot.Runs.Count(r => r.Status == "Waiting" && r.EngineId.StartsWith("FLEET-SIM-")) == waitsBeforePulse,
        "Live dashboard pulses do not bypass holds or required human waits");
    Verify((await new ProcessOperationsStore(host, operations).SnapshotAsync()).DemoTick == pulseSnapshot.DemoTick,
        "Dashboard progress and event ledger survive restart");

    // Process Intelligence: persisted time checkpoints, replay fidelity and
    // saved investigation views share the same underlying synthetic state.
    var intelligence = new FleetIntelligenceStore(host);
    await intelligence.SampleAsync(pulseSnapshot, fleetActions, mapBefore);
    var snapshots = await intelligence.TimelineAsync();
    Verify(snapshots.Count == 1 && snapshots[0].VisitCount == pulseSnapshot.Engines.Count,
        "Persisted historical checkpoint records the full synthetic fleet");
    var checkpoint = await intelligence.FrameAsync(snapshots[0].Id);
    Verify(checkpoint is not null && checkpoint.Operations.DemoTick == pulseSnapshot.DemoTick
           && checkpoint.Operations.Parts.Count == pulseSnapshot.Parts.Count,
        "Replayed checkpoint restores engine progress and component state without advancing the store");
    var reopenedHistory = new FleetIntelligenceStore(host);
    Verify((await reopenedHistory.FrameAsync(snapshots[0].Id))?.Operations.Engines.Count == pulseSnapshot.Engines.Count,
        "Timeline snapshots remain readable after server repository restart");
    var exampleView = new FleetInvestigationView
    {
        Name="Engineering waiting investigation", Scope="workspace",WorkspaceId="engineering",
        LevelId="repair",ProcessId="quality",Family="PW1100G-JM",Heatmap="pressure",
        SelectedProcesses=["findings","quality"],MultiMatch="union",
        Layout = new() { ["quality"]=new(){X=180,Y=265} },
        ViewCenter=new(){X=90,Y=35},ViewScale=.82
    };
    await intelligence.SaveViewAsync(exampleView);
    var saved=(await new FleetIntelligenceStore(host).ViewsAsync()).Single(v=>v.Id==exampleView.Id);
    Verify(saved.Layout["quality"].X==180 && saved.SelectedProcesses.Count==2,
        "Saved investigations preserve current graph layout, selection, scope and filters");
    await intelligence.DeleteViewAsync(exampleView.Id);
    Verify(!(await intelligence.ViewsAsync()).Any(v=>v.Id==exampleView.Id),
        "Saved investigations may be deleted without affecting workflows or engine data");
    var heldEngine=pulseSnapshot.Engines.First(e=>e.Id==heldDemo.EngineId);
    var blockedSignals=FleetInsights.BlockersFor(heldEngine,pulseSnapshot,fleetActions);
    Verify(blockedSignals.Any(b=>b.Kind=="Part hold"),
        "Explainability retains direct component hold evidence for a blocked repair visit");
    var segments=new[]{"findings","quality"};
    var union=pulseSnapshot.Engines.Count(e=>FleetVisitSlicer.MatchesProcesses(e,segments,"union",mapBefore));
    var both=pulseSnapshot.Engines.Count(e=>FleetVisitSlicer.MatchesProcesses(e,segments,"intersection",mapBefore));
    Verify(union>=both,"Multi-node union is a superset of intersection");
    var projections=FleetInsights.Forecast(mapBefore.Children("repair"),pulseSnapshot.Engines,mapBefore,100,4,85,100);
    Verify(projections.Count==8 && projections.All(p=>p.EstimatedDays>=0 && p.ScenarioDays>=0),
        "Scenario sandbox produces non-negative illustrative comparisons without changing any visit");
    Verify(projections.All(p=>p.SimulatedNewArrivals>0),
        "Forecast distributes fractional incoming visits without integer truncation");
    await intelligence.ResetTimelineAsync();
    Verify((await intelligence.TimelineAsync()).Count==0,
        "Reset erases demonstration replay history without modifying operational store");

    // Connected work-center/module scenario: one typed action item is bound to its
    // source finding and cannot bypass module completion with a status dropdown.
    var coordinator = new OperationalCoordinationService(processes, operations, processMap);
    var operational = await coordinator.ReadAsync();
    var outstanding = operational.Processes.Findings.First(f => f.Status == "Pending disposition" &&
        f.Id.StartsWith("FND-FLT-", StringComparison.Ordinal));
    var typedAction = operational.Action(outstanding.ActionId)!;
    Verify(typedAction.ModuleId == "findings" && typedAction.CapabilityId == "engineering.disposition" &&
        typedAction.SourceRecordId == outstanding.Id && typedAction.RequiresVerifiedCompletion,
        "Source-backed finding action exposes its module, command and completion event");
    var blockedManualClosure = false;
    try { await operations.SetStatusAsync(typedAction.Id, "Done"); }
    catch (InvalidOperationException) { blockedManualClosure = true; }
    Verify(blockedManualClosure && (await operations.SnapshotAsync()).Actions.Single(a=>a.Id==typedAction.Id).Status != "Done",
        "General action edits cannot fabricate engineering disposition completion");
    var result = await coordinator.SubmitDispositionAsync(outstanding.Id, true);
    operational = await coordinator.ReadAsync();
    var decided = operational.Processes.Findings.Single(f=>f.Id==outstanding.Id);
    Verify(result.Contains("Inspection action") && decided.Status == "Awaiting inspection" &&
        decided.InspectionActionId is not null,
        "Submitting a synthetic engineering decision creates a separate downstream inspection action");
    Verify(operational.Action(typedAction.Id)?.Status == "Done" &&
        operational.Action(decided.InspectionActionId!)?.ModuleId == "quality",
        "Module event completes engineering work and binds Quality's new typed responsibility");
    Verify(operational.Processes.Parts.Single(p=>p.Id==decided.PartId).Held,
        "Moving from Engineering to Quality does not silently release the affected component");
    await coordinator.RecordInspectionAsync(decided.Id, true);
    operational = await coordinator.ReadAsync();
    Verify(operational.Action(decided.InspectionActionId!)?.Status == "Done" &&
        operational.Processes.Runs.Single(run=>run.FindingId==decided.Id).Status == "Completed",
        "Independent simulated inspection event completes the Quality action and resumes the workflow");
    Verify(OperationalRouting.Atlas(outstanding.EngineId).Contains("mode=fleet") &&
        operational.WorkbenchUrl(typedAction).Contains("finding="),
        "Action, module and Atlas deep links preserve a shared operational context");
    var general = await coordinator.CreateGeneralActionAsync("Review simulated work package",
        "engineering","High",outstanding.EngineId,"u-jordan");
    operational=await coordinator.ReadAsync();
    Verify(operational.Action(general.Id)?.Assignments.Any(a=>a.PrincipalId=="u-jordan" && a.Role=="Responsible")==true,
        "Work Center creates a general task with an individual Responsible assignment");
    Verify(operational.Processes.Events.Any(e=>e.ActionId==general.Id && e.EventType=="action.created.demo"),
        "Creating a general task linked to an engine also creates a trace event");
    var siteRollup = OperationsRollups.Summarize(operational.Operations, "cec");
    Verify(siteRollup.Total == operational.Operations.Actions.Count,
        "Workspace parent rollups include source-linked tasks only once");

    await operations.ResetDemoAsync();
    data = await operations.SnapshotAsync();
    Verify(data.Actions.Count == 8, "Synthetic actions can be reset");
    Console.WriteLine("\nAll smoke checks passed.");
}
finally
{
    try { Directory.Delete(root, true); } catch (IOException) { }
}

sealed class TestEnvironment(string contentRoot) : IWebHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "NodalSmoke";
    public string WebRootPath { get; set; } = Path.Combine(contentRoot, "wwwroot");
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = contentRoot;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
