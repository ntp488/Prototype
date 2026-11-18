using Microsoft.AspNetCore.Hosting;
using System.Text.Json;
using NodalWorkflowLab.Models;

namespace NodalWorkflowLab.Services;

/// <summary>Durable editable hierarchy of process definitions. Does not publish live workflows.</summary>
public sealed class ProcessMapStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _path;
    private readonly WorkflowLibraryStore _library;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private ProcessMapSnapshot _map;

    public ProcessMapStore(IWebHostEnvironment environment, WorkflowLibraryStore library)
    {
        _library = library;
        _path = Path.Combine(environment.ContentRootPath, "App_Data", "process-map.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _map = File.Exists(_path)
            ? JsonSerializer.Deserialize<ProcessMapSnapshot>(File.ReadAllText(_path))
              ?? throw new InvalidDataException("The process-map store was not readable.")
            : ProcessMapSnapshot.Seed();
        Validate(_map);
        // Non-destructive catalog migration: add new deep process definitions to
        // existing saved maps without touching custom/user-authored definitions.
        var updated = false;
        var known = _map.Processes.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var canonical in ProcessCatalog.Processes)
        {
            if (known.Add(canonical.Id))
            {
                _map.Processes.Add(new ProcessDefinition {
                    Id = canonical.Id, Name = canonical.Name, ModuleId = canonical.ModuleId,
                    ParentId = canonical.ParentId, Description = canonical.Description,
                    WorkflowId = canonical.WorkflowId, Kind = canonical.Kind,
                    Version = canonical.Version, Applicability = canonical.Applicability,
                    X = canonical.X, Y = canonical.Y });
                updated = true;
            }
        }
        foreach (var canonical in ProcessCatalog.Links)
        {
            if (!_map.Links.Any(x => x.ParentId == canonical.ParentId && x.FromId == canonical.FromId && x.ToId == canonical.ToId))
            { _map.Links.Add(canonical); updated = true; }
        }
        var intake = _map.Processes.FirstOrDefault(x => x.Id == "induct");
        if (intake is not null && intake.Name == "Induct engine")
        { intake.Name = "Gate 1 · Plant intake"; intake.Description = ProcessCatalog.Get("induct").Description; updated = true; }
        if (intake is not null && intake.Applicability != "All engines") { intake.Applicability = "All engines"; updated = true; }
        if (intake is not null && string.IsNullOrEmpty(intake.WorkflowId)) { intake.WorkflowId = "gate1-intake-graph"; updated = true; }
        if (updated || !File.Exists(_path)) File.WriteAllText(_path, JsonSerializer.Serialize(_map, Options));
    }

    public async Task<ProcessMapSnapshot> SnapshotAsync()
    {
        await _gate.WaitAsync();
        try { return _map.Copy(); }
        finally { _gate.Release(); }
    }

    public async Task<ProcessDefinition> AddChildAsync(string parentId, string moduleId, string name,
        string description, string? predecessorId, string? applicability = null)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 90)
            throw new ArgumentException("Process name is required (maximum 90 characters).");
        if (description.Length > 700) throw new ArgumentException("Process description is too long.");
        if (!ProcessCatalog.Modules.Any(m => m.Id == moduleId)) throw new ArgumentException("Unknown site module.");
        await _gate.WaitAsync();
        try
        {
            if (!_map.Processes.Any(p => p.Id == parentId)) throw new ArgumentException("Unknown parent process.");
            if (!string.IsNullOrWhiteSpace(predecessorId) && !_map.Processes.Any(p => p.Id == predecessorId && p.ParentId == parentId))
                throw new ArgumentException("Predecessor must belong to this parent process.");
            var siblings = _map.Children(parentId);
            var inherited = parentId != "repair" &&
                _map.Processes.First(p => p.Id == parentId).Applicability == "All engines";
            var scope = applicability ?? (inherited ? "All engines" : "When applicable");
            if (scope != "All engines" && scope != "When applicable")
                throw new ArgumentException("Invalid process applicability.");
            var id = "custom-" + Guid.NewGuid().ToString("N")[..10];
            var workflowId = "process-" + id;
            // Every user-created sub-process immediately has an editable and executable workflow graph.
            var document = new WorkflowDocument
            {
                Id=workflowId, Name="Process / " + name.Trim(), Enabled=false,
                Description="Illustrative process workflow. Connect module queries, reusable workflows and actions as needed.",
                Graph = new WorkflowGraph
                {
                    Nodes =
                    [
                        WorkflowNode.Create("start-" + id, "trigger", 90, 210, ("message", "Process " + name.Trim())),
                        WorkflowNode.Create("end-" + id, "finish", 430, 210, ("label", "Step complete"))
                    ],
                    Edges = [new WorkflowEdge("edge-" + id, "start-" + id, "next", "end-" + id, "in")]
                }
            };
            await _library.SaveAsync(document);
            var additional = Math.Max(0, siblings.Count - 8);
            var element = new ProcessDefinition
            {
                Id=id, Name=name.Trim(), Description=description.Trim(), ModuleId=moduleId,
                ParentId=parentId, WorkflowId=workflowId,
                X=80 + (additional % 5) * 310,
                Y=(parentId == "repair" ? 410 : 340) + additional / 5 * 185,
                Kind="module process", Version="0.1-draft", Applicability=scope
            };
            _map.Processes.Add(element);
            if (!string.IsNullOrWhiteSpace(predecessorId))
                _map.Links.Add(new ProcessLink(parentId, predecessorId, id, "custom dependency"));
            await PersistAsync();
            return element;
        }
        finally { _gate.Release(); }
    }

    public async Task AddLinkAsync(string parentId, string fromId, string toId)
    {
        if (fromId == toId) throw new ArgumentException("A process cannot depend on itself.");
        await _gate.WaitAsync();
        try
        {
            if (!_map.Processes.Any(p => p.Id == fromId && p.ParentId == parentId) ||
                !_map.Processes.Any(p => p.Id == toId && p.ParentId == parentId))
                throw new ArgumentException("Both processes must belong to the same parent.");
            if (_map.Links.Any(l => l.ParentId == parentId && l.FromId == fromId && l.ToId == toId))
                throw new ArgumentException("This dependency already exists.");
            // Prevent cycles in the visual process dependency graph.
            var reachable = new HashSet<string>(StringComparer.Ordinal) { toId };
            var queue = new Queue<string>();
            queue.Enqueue(toId);
            while (queue.TryDequeue(out var current))
            {
                foreach (var next in _map.Links.Where(l => l.ParentId == parentId && l.FromId == current).Select(l => l.ToId))
                    if (reachable.Add(next)) queue.Enqueue(next);
            }
            if (reachable.Contains(fromId)) throw new InvalidOperationException("This dependency would introduce a cycle.");
            _map.Links.Add(new ProcessLink(parentId, fromId, toId, "custom dependency"));
            await PersistAsync();
        }
        finally { _gate.Release(); }
    }

    private static void Validate(ProcessMapSnapshot map)
    {
        if (map.Processes is null || map.Links is null || map.Processes.Count > 4000 || map.Links.Count > 12000 ||
            map.Processes.Any(p => string.IsNullOrWhiteSpace(p.Id)) ||
            map.Processes.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != map.Processes.Count)
            throw new InvalidDataException("Process map contains invalid definitions.");
        if (map.Processes.Any(p => p.ParentId is not null && !map.Processes.Any(x => x.Id == p.ParentId)))
            throw new InvalidDataException("Process map has a missing parent.");
        if (map.Links.Any(l => !map.Processes.Any(p => p.Id == l.FromId && p.ParentId == l.ParentId)
            || !map.Processes.Any(p => p.Id == l.ToId && p.ParentId == l.ParentId)))
            throw new InvalidDataException("Process map contains invalid dependencies.");
    }
    private async Task PersistAsync()
    {
        var tmp = _path + ".tmp";
        await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(_map, Options));
        File.Move(tmp, _path, overwrite:true);
    }
}
