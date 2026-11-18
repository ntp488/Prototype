using System.Text.Json;

namespace NodalWorkflowLab.Models;

/// <summary>Serializable version of the hierarchical process definition map (not an execution ledger).</summary>
public sealed class ProcessMapSnapshot
{
    public List<ProcessDefinition> Processes { get; set; } = [];
    public List<ProcessLink> Links { get; set; } = [];
    public ProcessMapSnapshot Copy() => JsonSerializer.Deserialize<ProcessMapSnapshot>(JsonSerializer.Serialize(this))!;
    public ProcessDefinition Get(string id) => Processes.First(x => x.Id == id);
    public List<ProcessDefinition> Children(string id) => Processes.Where(x => x.ParentId == id).ToList();
    public static ProcessMapSnapshot Seed() => new()
    {
        Processes = ProcessCatalog.Processes.Select(p => new ProcessDefinition
        {
            Id=p.Id, Name=p.Name, ModuleId=p.ModuleId, ParentId=p.ParentId,
            WorkflowId=p.WorkflowId, Version=p.Version, Description=p.Description,
            Kind=p.Kind, Applicability=p.Applicability, X=p.X, Y=p.Y
        }).ToList(),
        Links = ProcessCatalog.Links.ToList()
    };
}
