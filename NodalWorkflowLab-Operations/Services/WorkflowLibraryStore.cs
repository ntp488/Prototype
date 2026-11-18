using Microsoft.AspNetCore.Hosting;
using System.Text.Json;
using NodalWorkflowLab.Models;

namespace NodalWorkflowLab.Services;

/// <summary>
/// Server-wide, file-backed prototype repository. Files are under App_Data in the app content root.
/// No NuGet dependencies or browser-local workflow storage are required.
/// </summary>
public sealed class WorkflowLibraryStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _json = new() { WriteIndented = true };
    private List<WorkflowDocument> _items;

    public WorkflowLibraryStore(IWebHostEnvironment environment)
    {
        _path = Path.Combine(environment.ContentRootPath, "App_Data", "workflows.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _items = LoadOrSeed();
    }

    private List<WorkflowDocument> LoadOrSeed()
    {
        if (File.Exists(_path))
        {
            var documents = JsonSerializer.Deserialize<List<WorkflowDocument>>(File.ReadAllText(_path))
                ?? throw new InvalidDataException("Workflow library is empty or corrupt.");
            if (documents.Any(w => w.Graph is null || !w.Graph.Validate(out _)))
                throw new InvalidDataException("Workflow library contains an invalid graph.");
            // Non-destructive seed migration: preserve user-authored graphs and add only missing samples.
            var existingIds = documents.Select(w => w.Id).ToHashSet(StringComparer.Ordinal);
            var additions = WorkflowTemplates.Create().Where(w => !existingIds.Contains(w.Id)).ToList();
            if (additions.Count > 0)
            {
                documents.AddRange(additions);
                File.WriteAllText(_path, JsonSerializer.Serialize(documents, _json));
            }
            return documents;
        }
        var seeded = WorkflowTemplates.Create();
        File.WriteAllText(_path, JsonSerializer.Serialize(seeded, _json));
        return seeded;
    }

    public async Task<List<WorkflowDocument>> ListAsync()
    {
        await _gate.WaitAsync();
        try { return _items.Select(w => w.Copy()).OrderBy(w => w.Name).ToList(); }
        finally { _gate.Release(); }
    }

    public async Task<WorkflowDocument?> GetAsync(string id)
    {
        await _gate.WaitAsync();
        try { return _items.FirstOrDefault(w => w.Id == id)?.Copy(); }
        finally { _gate.Release(); }
    }

    public async Task SaveAsync(WorkflowDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.Id) || string.IsNullOrWhiteSpace(document.Name) ||
            document.Name.Length > 100 || document.Description.Length > 1000 ||
            document.Graph is null || !document.Graph.Validate(out var issue))
            throw new InvalidDataException("Invalid workflow document (check its name and graph).");
        await _gate.WaitAsync();
        try
        {
            var copy = document.Copy();
            copy.UpdatedUtc = DateTimeOffset.UtcNow;
            var index = _items.FindIndex(w => w.Id == copy.Id);
            if (index >= 0) _items[index] = copy;
            else _items.Add(copy);
            await WriteUnsafeAsync();
            document.UpdatedUtc = copy.UpdatedUtc;
        }
        finally { _gate.Release(); }
    }

    public async Task DeleteAsync(string id)
    {
        await _gate.WaitAsync();
        try
        {
            if (_items.Count <= 1) throw new InvalidOperationException("Keep at least one workflow.");
            _items.RemoveAll(w => w.Id == id);
            await WriteUnsafeAsync();
        }
        finally { _gate.Release(); }
    }

    private async Task WriteUnsafeAsync()
    {
        var temp = _path + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(_items, _json));
        File.Move(temp, _path, true);
    }
}
