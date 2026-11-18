using System.Text.Json;

namespace NodalWorkflowLab.Models;

// All operational identifiers, serials, conditions and approval results are invented.
// Product-family relationships and high-level engine architecture are publicly documented.
public sealed record SiteModule(string Id, string Name, string Icon, string Description, string Color,
    string[] Commands, string[] Events);

public sealed class ProcessDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ModuleId { get; set; } = "";
    public string Description { get; set; } = "";
    public string? ParentId { get; set; }
    public string? WorkflowId { get; set; }
    public string Version { get; set; } = "0.1-draft";
    public string Kind { get; set; } = "workflow";
    public string Applicability { get; set; } = "When applicable";
    public int X { get; set; }
    public int Y { get; set; }
}

public sealed record ProcessLink(string ParentId, string FromId, string ToId, string Label = "");

public static class ProcessCatalog
{
    public static readonly IReadOnlyList<SiteModule> Modules =
    [
        new("induction", "Induction", "IN", "Inbound records and work-order coordination", "#70a7e8", ["Record induction", "Verify intake"], ["Engine received"]),
        new("teardown", "Teardown", "TD", "Controlled disassembly and component traceability", "#8aa7e7", ["Record removal", "Report condition"], ["Component removed"]),
        new("findings", "Findings", "FN", "In-process findings and engineering disposition", "#edab79", ["Report finding", "Request disposition"], ["Finding reported", "Disposition recorded"]),
        new("parts", "Parts logistics", "PT", "Scan and locate serialized components", "#70c5bf", ["Scan movement", "Place hold", "Clear hold"], ["Part moved", "Movement rejected"]),
        new("quality", "Quality", "QA", "Controlled inspection requests and recorded sign-offs", "#be9ae9", ["Request inspection", "Record demonstration sign-off"], ["Inspection recorded"]),
        new("rebuild", "Rebuild", "RB", "Assembly readiness and component installation history", "#7cce9d", ["Check readiness", "Start rebuild"], ["Rebuild started"]),
        new("test", "Engine testing", "TS", "Test readiness and result capture", "#dfbd7d", ["Schedule test", "Record test"], ["Test completed"]),
        new("release", "Release readiness", "RL", "Non-authoritative readiness overview (never airworthiness approval)", "#9daac6", ["Check outstanding gates"], ["Readiness changed"])
    ];

    public static readonly IReadOnlyList<ProcessDefinition> Processes =
    [
        P("repair", "Engine repair · site process map", "induction", null, 45, 40, "Site-wide process architecture for all incoming engines. Engine family requirements may introduce additional branches.", "end-to-end-repair"),
        P("induct", "Gate 1 · Plant intake", "induction", "repair", 80, 110, "Universal entry gate for every engine received at the plant, independent of model or repair work scope.", "gate1-intake-graph"),
        P("teardown", "Teardown engine", "teardown", "repair", 375, 110, "Disassemble components and report observed conditions."),
        P("findings", "Resolve in-process finding", "findings", "repair", 670, 45, "Cross-module finding, hold and approval process.", "finding-resolution-graph"),
        P("materials", "Track and route parts", "parts", "repair", 670, 255, "Barcode-driven component location and hold checks.", "parts-movement-graph"),
        P("quality", "Inspection gate", "quality", "repair", 975, 45, "Await independent inspection evidence; do not auto-sign.", "inspection-gate-graph"),
        P("rebuild", "Rebuild engine", "rebuild", "repair", 1270, 135, "Release eligible parts into controlled assembly."),
        P("test", "Engine test", "test", "repair", 1560, 135, "Test scheduling, readiness and results."),
        P("release", "Release readiness", "release", "repair", 1850, 135, "Readiness summary only; no actual release to service."),

        // Universal Gate 1. These nodes describe a possible process, not site-approved operating instructions.
        P("intake-receive", "Receive engine at site", "induction", "induct", 80, 140, "Record receipt, the inbound visit, arrival time and physical location."),
        P("intake-identify", "Verify identity and configuration", "induction", "induct", 380, 140, "Correlate engine identity, configuration and applicable records."),
        P("intake-docs", "Reconcile received records", "induction", "induct", 680, 140, "Record availability and completeness of incoming documentation."),
        P("intake-scope", "Establish visit scope", "induction", "induct", 980, 140, "Capture the intended work scope and route dependencies."),
        P("intake-route", "Route to next process", "induction", "induct", 1280, 140, "Assign the next work area when the intake prerequisites are satisfied."),
        P("intake-exception", "Route intake exception", "quality", "induct", 980, 350, "Optional exception or hold path when intake verification needs review."),

        P("finding-capture", "Capture finding", "findings", "findings", 80, 140, "Create a synthetic finding and related action item."),
        P("finding-hold", "Hold affected part", "parts", "findings", 365, 140, "Prevent simulated physical movement."),
        P("finding-assign", "Assign engineering work", "findings", "findings", 650, 140, "Route responsibility to Repair Engineering."),
        P("finding-wait", "Wait for disposition", "findings", "findings", 935, 140, "Durable event wait for a simulated human decision."),
        P("finding-inspect", "Request inspection", "quality", "findings", 1220, 140, "Create the quality gate after accepted disposition."),
        P("finding-clear", "Await clearance", "quality", "findings", 1505, 140, "Resume only when the demonstration inspection is recorded."),

        P("part-identify", "Scan barcode", "parts", "materials", 80, 140, "Resolve a tracked serialized component."),
        P("part-check", "Check holds", "parts", "materials", 380, 140, "Deny movements while a part has active holds."),
        P("part-move", "Record movement", "parts", "materials", 680, 140, "Capture authorized movement and destination."),
        P("part-publish", "Publish movement event", "parts", "materials", 980, 140, "Append audit event and notify waiting processes."),

        P("inspect-request", "Request inspection", "quality", "quality", 80, 140, "Create the required inspection task."),
        P("inspect-wait", "Wait for human record", "quality", "quality", 400, 140, "Wait persistently for an inspection outcome."),
        P("inspect-evidence", "Validate simulated evidence", "quality", "quality", 740, 140, "Check that an independent record exists."),
        P("inspect-complete", "Publish clearance event", "quality", "quality", 1080, 140, "Resume subscribed processes; not an airworthiness approval."),

        // Richer site-wide process library. The decomposition is illustrative and does not
        // constitute an approved shop procedure or model-specific maintenance instruction.
        P("td-prepare", "Prepare teardown cell", "teardown", "teardown", 90, 145, "Reserve location, tooling and work documentation."),
        P("td-disassemble", "Disassembly operations", "teardown", "teardown", 390, 145, "Track component removals with serialized associations."),
        P("td-verify", "Record observed conditions", "teardown", "teardown", 690, 145, "Record observed discrepancies and link findings."),
        P("td-transfer", "Transfer to downstream work", "parts", "teardown", 990, 145, "Establish component destination and handoff records."),
        P("td-fan", "Fan and inlet group", "teardown", "td-disassemble", 90, 150, "Record teardown of eligible fan/inlet hardware."),
        P("td-compressor", "Compressor group", "teardown", "td-disassemble", 390, 150, "Record applicable compressor work."),
        P("td-combustor", "Combustor group", "teardown", "td-disassemble", 690, 150, "Capture combustor-related component records."),
        P("td-turbine", "Turbine group", "teardown", "td-disassemble", 990, 150, "Capture turbine group disassembly."),
        P("td-hpc", "High-pressure compressor", "teardown", "td-compressor", 90, 150, "Record individual HPC component work."),
        P("td-lpc", "Low-pressure compressor", "teardown", "td-compressor", 390, 150, "Record individual LPC component work."),
        P("td-compressor-index", "Serialize removal evidence", "parts", "td-compressor", 690, 150, "Index component removal evidence and tracking records."),
        P("mat-identify", "Incoming material verification", "parts", "materials", 1280, 140, "Reconcile receiving and serialized part identity."),
        P("mat-reserve", "Reserve for engine visit", "parts", "materials", 1580, 140, "Bind component availability to the visit."),
        P("qa-prep", "Prepare inspection package", "quality", "quality", 1440, 140, "Collect linked records and approved instructions."),
        P("qa-review", "Independent quality review", "quality", "quality", 1740, 140, "Inspect required evidence through an authorized process."),
        P("qa-evidence", "Review evidence completeness", "quality", "qa-review", 90, 150, "Check required records and signer authority."),
        P("qa-decision", "Record disposition", "quality", "qa-review", 390, 150, "Bind independent decision to evidence and version."),
        P("rb-readiness", "Verify rebuild readiness", "rebuild", "rebuild", 80, 145, "Check hold and parts eligibility state."),
        P("rb-stage", "Stage components", "parts", "rebuild", 380, 145, "Reserve eligible items and confirm location."),
        P("rb-assembly", "Controlled assembly", "rebuild", "rebuild", 680, 145, "Record installation associations and work events."),
        P("rb-inspections", "Assembly inspection gates", "quality", "rebuild", 980, 145, "Await required inspection records."),
        P("rb-complete", "Assembly reconciliation", "rebuild", "rebuild", 1280, 145, "Check completion and remaining exceptions."),
        P("rb-fan", "Fan group installation", "rebuild", "rb-assembly", 90, 150, "Track applicable fan installation."),
        P("rb-core", "Core module installation", "rebuild", "rb-assembly", 390, 150, "Track applicable core installation."),
        P("rb-external", "External hardware", "rebuild", "rb-assembly", 690, 150, "Track accessories and external hardware."),
        P("rb-records", "Reconcile install records", "rebuild", "rb-assembly", 990, 150, "Confirm component genealogy records exist."),
        P("rb-core-hpc", "HPC installation record", "rebuild", "rb-core", 90, 150, "Associate serialized compressor record."),
        P("rb-core-hpt", "HPT installation record", "rebuild", "rb-core", 390, 150, "Associate serialized turbine record."),
        P("test-plan", "Plan test cell visit", "test", "test", 90, 145, "Prepare synthetic test slot and resources."),
        P("test-eligibility", "Readiness screening", "quality", "test", 390, 145, "Verify required prerequisites are recorded."),
        P("test-run", "Conduct test sequence", "test", "test", 690, 145, "Track test process events and captures."),
        P("test-review", "Review test results", "test", "test", 990, 145, "Assess outcome and downstream routing."),
        P("test-rework", "Open rework if required", "findings", "test", 1290, 325, "Create exception and investigation when necessary."),
        P("test-calibration", "Verify cell readiness", "test", "test-run", 90, 150, "Confirm available cell status and required test setup."),
        P("test-monitor", "Record test milestones", "test", "test-run", 390, 150, "Track sequence state without simulating real sensor values."),
        P("test-package", "Assemble test evidence", "test", "test-run", 690, 150, "Bind test records to the visit."),
        P("rel-reconcile", "Reconcile visit records", "release", "release", 90, 145, "Review remaining visit and asset record gaps."),
        P("rel-exceptions", "Outstanding exception review", "quality", "release", 390, 145, "Locate unresolved holds, findings, and actions."),
        P("rel-authorization", "Authorized release review", "quality", "release", 690, 145, "Request human determination in authoritative system; this demo never signs."),
        P("rel-close", "Close simulated visit", "release", "release", 990, 145, "Mark the demonstration visit complete for dashboarding.")
    ];

    public static readonly IReadOnlyList<ProcessLink> Links =
    [
        L("repair", "induct", "teardown"), L("repair", "teardown", "findings", "if finding"),
        L("repair", "teardown", "materials", "parts removed"), L("repair", "findings", "quality"),
        L("repair", "materials", "rebuild"), L("repair", "quality", "rebuild"),
        L("repair", "rebuild", "test"), L("repair", "test", "release"),
        L("induct", "intake-receive", "intake-identify"), L("induct", "intake-identify", "intake-docs"),
        L("induct", "intake-docs", "intake-scope"), L("induct", "intake-scope", "intake-route"),
        L("induct", "intake-docs", "intake-exception", "if records require review"),
        L("findings", "finding-capture", "finding-hold"), L("findings", "finding-hold", "finding-assign"),
        L("findings", "finding-assign", "finding-wait"), L("findings", "finding-wait", "finding-inspect"),
        L("findings", "finding-inspect", "finding-clear"),
        L("materials", "part-identify", "part-check"), L("materials", "part-check", "part-move"),
        L("materials", "part-move", "part-publish"),
        L("quality", "inspect-request", "inspect-wait"), L("quality", "inspect-wait", "inspect-evidence"),
        L("quality", "inspect-evidence", "inspect-complete"),

        L("teardown", "td-prepare", "td-disassemble"), L("teardown", "td-disassemble", "td-verify"),
        L("teardown", "td-verify", "td-transfer"),
        L("td-disassemble", "td-fan", "td-compressor"), L("td-disassemble", "td-compressor", "td-combustor"),
        L("td-disassemble", "td-combustor", "td-turbine"),
        L("td-compressor", "td-hpc", "td-compressor-index"), L("td-compressor", "td-lpc", "td-compressor-index"),
        L("materials", "part-identify", "mat-identify"), L("materials", "mat-identify", "part-check"),
        L("materials", "part-publish", "mat-reserve"),
        L("quality", "inspect-request", "qa-prep"), L("quality", "qa-prep", "inspect-wait"),
        L("quality", "inspect-complete", "qa-review"), L("qa-review", "qa-evidence", "qa-decision"),
        L("rebuild", "rb-readiness", "rb-stage"), L("rebuild", "rb-stage", "rb-assembly"),
        L("rebuild", "rb-assembly", "rb-inspections"), L("rebuild", "rb-inspections", "rb-complete"),
        L("rb-assembly", "rb-fan", "rb-core"), L("rb-assembly", "rb-core", "rb-external"),
        L("rb-assembly", "rb-external", "rb-records"), L("rb-core", "rb-core-hpc", "rb-core-hpt"),
        L("test", "test-plan", "test-eligibility"), L("test", "test-eligibility", "test-run"),
        L("test", "test-run", "test-review"), L("test", "test-review", "test-rework", "if failed"),
        L("test-run", "test-calibration", "test-monitor"), L("test-run", "test-monitor", "test-package"),
        L("release", "rel-reconcile", "rel-exceptions"), L("release", "rel-exceptions", "rel-authorization"),
        L("release", "rel-authorization", "rel-close")
    ];

    private static ProcessDefinition P(string id, string name, string module, string? parent, int x, int y,
        string description, string? workflow = null) => new()
        { Id = id, Name = name, ModuleId = module, ParentId = parent, X = x, Y = y,
            Description = description, WorkflowId = workflow, Kind = parent == "repair" ? "module process" : "workflow",
            Applicability = id == "repair" || id == "induct" || id.StartsWith("intake-") && id != "intake-exception"
                ? "All engines" : "When applicable" };
    private static ProcessLink L(string parent, string from, string to, string label = "") => new(parent, from, to, label);

    public static ProcessDefinition Get(string id) => Processes.First(x => x.Id == id);
    public static SiteModule Module(string id) => Modules.First(x => x.Id == id);
    public static IReadOnlyList<ProcessDefinition> Children(string id) => Processes.Where(x => x.ParentId == id).ToList();
    public static string Path(string id) => Get(id).ParentId is { } parent ? Path(parent) + " / " + Get(id).Name : Get(id).Name;
}

public sealed class EngineCase
{
    public string Id { get; set; } = "";
    public string Family { get; set; } = "";
    public string Aircraft { get; set; } = "";
    public string WorkOrder { get; set; } = "";
    public string? IntakeActionId { get; set; }
    public string Customer { get; set; } = "Demo operator";
    public string Scope { get; set; } = "Demonstration repair visit";
    public DateTimeOffset ReceivedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset TargetUtc { get; set; } = DateTimeOffset.UtcNow.AddDays(25);
    public string Cell { get; set; } = "";
    public string Narrative { get; set; } = "";
    public Dictionary<string, string> Stages { get; set; } = [];
    // 0-99% simulated in-flight work; completed stages are always 100%.
    // Distinct from authoritative inspections or other human-controlled decisions.
    public Dictionary<string, int> StageProgress { get; set; } = [];
}

public sealed class TrackedPart
{
    public string Id { get; set; } = "";
    public string EngineId { get; set; } = "";
    public string Description { get; set; } = "";
    public string Module { get; set; } = "";
    public string Location { get; set; } = "";
    public bool Held { get; set; }
}

public sealed class ProcessFinding
{
    public string Id { get; set; } = "";
    public string EngineId { get; set; } = "";
    public string PartId { get; set; } = "";
    public string Description { get; set; } = "";
    public string ActionId { get; set; } = "";
    public string? InspectionActionId { get; set; }
    public string Status { get; set; } = "Pending disposition";
    public string? DispositionBy { get; set; }
    public string? InspectionBy { get; set; }
}

public sealed class ProcessRun
{
    public string Id { get; set; } = "";
    public string EngineId { get; set; } = "";
    public string ProcessId { get; set; } = "findings";
    public string Version { get; set; } = "0.1-draft";
    public string FindingId { get; set; } = "";
    public string Status { get; set; } = "Waiting";
    public string WaitingFor { get; set; } = "engineering.disposition.recorded";
    public DateTimeOffset StartedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ProcessAuditEvent
{
    public string Id { get; set; } = "";
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public string EngineId { get; set; } = "";
    public string? PartId { get; set; }
    public string? ActionId { get; set; }
    public string? RunId { get; set; }
    public string ModuleId { get; set; } = "";
    public string EventType { get; set; } = "";
    public string Detail { get; set; } = "";
    public string Actor { get; set; } = "simulated-system";
}

public sealed class ProcessSnapshot
{
    public int DemoTick { get; set; }
    public List<EngineCase> Engines { get; set; } = [];
    public List<TrackedPart> Parts { get; set; } = [];
    public List<ProcessFinding> Findings { get; set; } = [];
    public List<ProcessRun> Runs { get; set; } = [];
    public List<ProcessAuditEvent> Events { get; set; } = [];
    public ProcessSnapshot Copy() => JsonSerializer.Deserialize<ProcessSnapshot>(JsonSerializer.Serialize(this))!;

    public static ProcessSnapshot Seed()
    {
        var now = DateTimeOffset.UtcNow;
        var initial = new ProcessSnapshot
        {
            Engines =
            [
                new() { Id="PW-DEMO-1007", Family="PW1100G-JM", Aircraft="Airbus A320neo family", WorkOrder="WO-SIM-4107", Cell="Teardown Cell T-02",
                    Narrative="Illustrative fan-drive-area finding; engineering disposition is pending.",
                    Stages = Stages("Complete","In progress","Waiting","In progress","Not started","Blocked","Not started","Not started") },
                new() { Id="PW-DEMO-1012", Family="PW1500G", Aircraft="Airbus A220 family", WorkOrder="WO-SIM-4112", Cell="Rebuild Cell R-01",
                    Narrative="Illustrative clean-flow rebuild candidate with no active finding.",
                    Stages = Stages("Complete","Complete","Complete","Complete","Complete","Ready","Not started","Not started") },
                new() { Id="PW-DEMO-1024", Family="PW1900G", Aircraft="Embraer E-Jets E2 family", WorkOrder="WO-SIM-4124", Cell="Induction Bay I-03",
                    Narrative="Illustrative engine currently moving through universal Gate 1 intake.",
                    Stages = Stages("In progress","Not started","Not started","Not started","Not started","Not started","Not started","Not started") }
            ],
            Parts =
            [
                Part("SIM-PART-FDGS-011", "PW-DEMO-1007", "Fan drive gear system assembly", "Fan drive", "Quarantine rack Q-04", true),
                Part("SIM-PART-HPC-022", "PW-DEMO-1007", "High-pressure compressor module", "HPC", "Teardown Cell T-02", false),
                Part("SIM-PART-HPT-023", "PW-DEMO-1007", "High-pressure turbine module", "HPT", "Work-in-process rack W-02", false),
                Part("SIM-PART-FAN-031", "PW-DEMO-1012", "Fan assembly", "Fan", "Rebuild Cell R-01", false),
                Part("SIM-PART-LPT-032", "PW-DEMO-1012", "Low-pressure turbine module", "LPT", "Rebuild Cell R-01", false),
                Part("SIM-PART-LPC-041", "PW-DEMO-1024", "Low-pressure compressor module", "LPC", "Induction Bay I-03", false)
            ],
            Findings =
            [
                new() { Id="FND-SIM-001", EngineId="PW-DEMO-1007", PartId="SIM-PART-FDGS-011",
                    Description="Training scenario: inspection observation requires engineering assessment; no dimensions or repair instructions included.",
                    ActionId="ACT-1002", Status="Pending disposition" }
            ],
            Runs =
            [
                new() { Id="RUN-SIM-001", EngineId="PW-DEMO-1007", FindingId="FND-SIM-001", Status="Waiting",
                    WaitingFor="engineering.disposition.recorded", StartedUtc=now.AddHours(-2), UpdatedUtc=now.AddMinutes(-35) }
            ],
            Events =
            [
                Event("PW-DEMO-1007", "induction", "engine.inducted", "Simulated intake documentation correlated with work order.", now.AddHours(-7)),
                Event("PW-DEMO-1007", "teardown", "teardown.started", "Engine entered Teardown Cell T-02.", now.AddHours(-5)),
                Event("PW-DEMO-1007", "parts", "part.removed", "Fan-drive-area assembly correlated to fictional component record.", now.AddHours(-3), "SIM-PART-FDGS-011"),
                Event("PW-DEMO-1007", "findings", "finding.reported", "FND-SIM-001 reported; engineering action ACT-1002 linked.", now.AddHours(-2), "SIM-PART-FDGS-011", "ACT-1002", "RUN-SIM-001"),
                Event("PW-DEMO-1007", "parts", "part.held", "Demonstration hold applied to component.", now.AddHours(-2).AddSeconds(1), "SIM-PART-FDGS-011", "ACT-1002", "RUN-SIM-001"),
                Event("PW-DEMO-1007", "workflow", "workflow.waiting", "RUN-SIM-001 waiting for engineering.disposition.recorded.", now.AddMinutes(-35), "SIM-PART-FDGS-011", "ACT-1002", "RUN-SIM-001"),
                Event("PW-DEMO-1012", "rebuild", "rebuild.ready", "Synthetic readiness checks completed.", now.AddMinutes(-20)),
                Event("PW-DEMO-1024", "induction", "engine.inducted", "Synthetic intake record prepared.", now.AddHours(-1))
            ]
        };
        foreach (var engine in initial.Engines)
        {
            bool incoming = engine.Id == "PW-DEMO-1024";
            string[] steps = ["intake-receive", "intake-identify", "intake-docs", "intake-scope", "intake-route"];
            foreach (var (step, index) in steps.Select((value, index) => (value, index)))
                engine.Stages[step] = incoming ? index == 0 ? "Complete" : index == 1 ? "Ready" : "Not started" : "Complete";
            engine.Stages["intake-exception"] = "Not applicable";
        }
        return initial;
    }

    private static Dictionary<string,string> Stages(params string[] values)
    {
        string[] names = ["induct","teardown","findings","materials","quality","rebuild","test","release"];
        return names.Zip(values).ToDictionary(x => x.First, x => x.Second);
    }
    private static TrackedPart Part(string id, string engine, string description, string module, string location, bool held) =>
        new() { Id=id, EngineId=engine, Description=description, Module=module, Location=location, Held=held };
    private static ProcessAuditEvent Event(string engine, string module, string type, string detail, DateTimeOffset date,
        string? part = null, string? action = null, string? run = null) =>
        new() { Id="EVT-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(), EngineId=engine, ModuleId=module,
            EventType=type, Detail=detail, TimestampUtc=date, PartId=part, ActionId=action, RunId=run };
}
