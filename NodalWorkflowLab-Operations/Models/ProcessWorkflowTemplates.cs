namespace NodalWorkflowLab.Models;

/// <summary>
/// Representative, executable node compositions. The site-process simulation uses its own
/// persisted event state machine; these diagrams illustrate composition and module queries.
/// </summary>
public static class ProcessWorkflowTemplates
{
    public static List<WorkflowDocument> Create() =>
    [
        new()
        {
            Id="end-to-end-repair", Name="Site / Engine repair orchestration", Enabled=false,
            Description="Composite graph demonstrating reusable subworkflow calls. It is a planning model, not an approved engine repair route.",
            Graph = new()
            {
                Nodes =
                [
                    WorkflowNode.Create("site-start","trigger",90,230,("message","Simulated site process launched")),
                    WorkflowNode.Create("site-intake","subworkflow",370,230,("workflowId","gate1-intake-graph")),
                    WorkflowNode.Create("site-read","site-query",680,230,("query","engine-summary"),("engineId","PW-DEMO-1007")),
                    WorkflowNode.Create("site-finding","subworkflow",980,230,("workflowId","finding-resolution-graph")),
                    WorkflowNode.Create("site-parts","subworkflow",1280,230,("workflowId","parts-movement-graph")),
                    WorkflowNode.Create("site-inspection","subworkflow",1580,230,("workflowId","inspection-gate-graph")),
                    WorkflowNode.Create("site-complete","finish",1880,230,("label","Process composition demonstration complete"))
                ],
                Edges =
                [
                    new("site-e0","site-start","next","site-intake","in"),
                    new("site-e1","site-intake","next","site-read","in"),
                    new("site-e2","site-read","next","site-finding","in"),
                    new("site-e3","site-finding","next","site-parts","in"),
                    new("site-e4","site-parts","next","site-inspection","in"),
                    new("site-e5","site-inspection","next","site-complete","in")
                ]
            }
        },
        new()
        {
            Id="gate1-intake-graph", Name="Induction / Universal Gate 1 intake", Enabled=false,
            Description="Universal plant receipt and intake process for all engine families. Planning demonstration only; actual receiving rules remain in authoritative modules.",
            Graph = new()
            {
                Nodes =
                [
                    WorkflowNode.Create("g-start","trigger",80,220,("message","Gate 1 intake invoked")),
                    WorkflowNode.Create("g-query","site-query",390,220,("query","engine-summary"),("engineId","PW-DEMO-1024")),
                    WorkflowNode.Create("g-log","log",700,220,("label","Record intake context")),
                    WorkflowNode.Create("g-end","finish",1010,220,("label","Intake overview complete"))
                ],
                Edges =
                [
                    new("g-e1","g-start","next","g-query","in"),
                    new("g-e2","g-query","next","g-log","in"),
                    new("g-e3","g-log","next","g-end","in")
                ]
            }
        },
        new()
        {
            Id="finding-resolution-graph", Name="Findings / Resolution overview", Enabled=false,
            Description="Query in-process findings through the shared site-module interface (read-only demo). Real waits are modeled in Process Explorer.",
            Graph = new()
            {
                Nodes =
                [
                    WorkflowNode.Create("f-start","trigger",80,200,("message","Finding overview")),
                    WorkflowNode.Create("f-query","site-query",390,200,("query","waiting-executions"),("engineId","PW-DEMO-1007")),
                    WorkflowNode.Create("f-log","log",700,200,("label","Pending finding workflows")),
                    WorkflowNode.Create("f-finish","finish",1010,200,("label","Finding module overview"))
                ],
                Edges =
                [
                    new("f-e1","f-start","next","f-query","in"),
                    new("f-e2","f-query","next","f-log","in"),
                    new("f-e3","f-log","next","f-finish","in")
                ]
            }
        },
        new()
        {
            Id="parts-movement-graph", Name="Parts / Barcode movement overview", Enabled=false,
            Description="Read part locations and holds through the site-module query interface.",
            Graph = new()
            {
                Nodes =
                [
                    WorkflowNode.Create("p-start","trigger",80,200,("message","Part routing overview")),
                    WorkflowNode.Create("p-query","site-query",390,200,("query","part-status"),("engineId","PW-DEMO-1007")),
                    WorkflowNode.Create("p-log","log",700,200,("label","Parts and holds")),
                    WorkflowNode.Create("p-end","finish",1010,200,("label","Parts overview complete"))
                ],
                Edges =
                [
                    new("p-e1","p-start","next","p-query","in"),
                    new("p-e2","p-query","next","p-log","in"),
                    new("p-e3","p-log","next","p-end","in")
                ]
            }
        },
        new()
        {
            Id="inspection-gate-graph", Name="Quality / Inspection gate overview", Enabled=false,
            Description="Inspect pending workflow gates; no electronic signature or approval is performed.",
            Graph = new()
            {
                Nodes =
                [
                    WorkflowNode.Create("i-start","trigger",80,200,("message","Inspection gate overview")),
                    WorkflowNode.Create("i-query","site-query",390,200,("query","waiting-executions"),("engineId","PW-DEMO-1007")),
                    WorkflowNode.Create("i-log","log",700,200,("label","Waiting inspection gates")),
                    WorkflowNode.Create("i-end","finish",1010,200,("label","Inspection overview complete"))
                ],
                Edges =
                [
                    new("i-e1","i-start","next","i-query","in"),
                    new("i-e2","i-query","next","i-log","in"),
                    new("i-e3","i-log","next","i-end","in")
                ]
            }
        }
    ];
}
