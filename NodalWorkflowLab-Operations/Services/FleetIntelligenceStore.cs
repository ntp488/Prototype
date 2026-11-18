using System.IO.Compression;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using NodalWorkflowLab.Models;

namespace NodalWorkflowLab.Services;

/// <summary>
/// Bounded, persisted, point-in-time synthetic snapshots and named investigation views.
/// Files deliberately remain outside wwwroot. No user identity/access-control is implemented;
/// never use this prototype store with protected or regulated information.
/// </summary>
public sealed class FleetIntelligenceStore
{
    private readonly SemaphoreSlim _gate = new(1,1);
    private readonly string _directory;
    private readonly string _indexFile;
    private readonly string _viewsFile;
    private readonly JsonSerializerOptions _options = new() { WriteIndented = false };
    private readonly List<FleetHistoryIndex> _index;
    private readonly List<FleetInvestigationView> _views;
    private static readonly TimeSpan MinimumSample = TimeSpan.FromSeconds(8);
    private const int MaxSamples = 180;
    private const int MaxViews = 60;

    public FleetIntelligenceStore(IWebHostEnvironment env)
    {
        _directory = Path.Combine(env.ContentRootPath, "App_Data", "fleet-history");
        Directory.CreateDirectory(_directory);
        _indexFile = Path.Combine(_directory, "index.json");
        _viewsFile = Path.Combine(_directory,"views.json");
        _index = ReadJson<List<FleetHistoryIndex>>(_indexFile) ?? [];
        _views = ReadJson<List<FleetInvestigationView>>(_viewsFile) ?? [];
        _index.RemoveAll(i => !File.Exists(FramePath(i.Id)));
    }
    private T? ReadJson<T>(string path) => File.Exists(path)
        ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), _options) : default;
    private string FramePath(string id) => Path.Combine(_directory,id+".json.gz");
    private async Task SaveJsonAsync<T>(string path,T value)
    {
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary,JsonSerializer.Serialize(value,_options));
        File.Move(temporary,path,true);
    }
    public async Task SampleAsync(ProcessSnapshot operations, OperationsSnapshot actions, ProcessMapSnapshot map)
    {
        await _gate.WaitAsync();
        try
        {
            var now = DateTimeOffset.UtcNow;
            if (_index.Count > 0 && now - _index[^1].CapturedUtc < MinimumSample) return;
            var frame = new FleetHistoryFrame { CapturedUtc=now, Operations=operations.Copy(), Actions=actions.Copy() };
            var id = now.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
            await using (var file = File.Create(FramePath(id)))
            await using (var gzip = new GZipStream(file,CompressionLevel.Fastest))
                await JsonSerializer.SerializeAsync(gzip,frame,_options);
            var info = new FleetHistoryIndex { Id=id,CapturedUtc=now,VisitCount=operations.Engines.Count,
                HoldCount=operations.Parts.Count(x=>x.Held) };
            foreach (var p in map.Processes)
            {
                info.Queues[p.Id] = operations.Engines.Count(e => FleetAnalytics.Status(e,p,map) is "Ready" or "In progress" or "Blocked" or "Waiting");
                info.Completed[p.Id] = operations.Engines.Count(e => FleetAnalytics.Status(e,p,map) == "Complete");
            }
            _index.Add(info);
            while (_index.Count > MaxSamples)
            {
                var old = _index[0];_index.RemoveAt(0);
                if (File.Exists(FramePath(old.Id))) File.Delete(FramePath(old.Id));
            }
            await SaveJsonAsync(_indexFile,_index);
        }
        finally { _gate.Release(); }
    }
    public async Task<List<FleetHistoryIndex>> TimelineAsync()
    {
        await _gate.WaitAsync();try { return _index.Select(x=>x).ToList(); }
        finally { _gate.Release(); }
    }
    public async Task<FleetHistoryFrame?> FrameAsync(string id)
    {
        await _gate.WaitAsync();
        try
        {
            if (!_index.Any(x=>x.Id==id) || !File.Exists(FramePath(id))) return null;
            await using var file=File.OpenRead(FramePath(id));
            await using var gzip=new GZipStream(file,CompressionMode.Decompress);
            return await JsonSerializer.DeserializeAsync<FleetHistoryFrame>(gzip,_options);
        }
        finally { _gate.Release(); }
    }
    public async Task ResetTimelineAsync()
    {
        await _gate.WaitAsync();
        try
        {
            foreach (var point in _index)
                if (File.Exists(FramePath(point.Id))) File.Delete(FramePath(point.Id));
            _index.Clear();await SaveJsonAsync(_indexFile,_index);
        }
        finally { _gate.Release(); }
    }
    public async Task<List<FleetInvestigationView>> ViewsAsync()
    {
        await _gate.WaitAsync(); try {return _views.OrderByDescending(v=>v.SavedUtc).ToList();}
        finally {_gate.Release();}
    }
    public async Task SaveViewAsync(FleetInvestigationView view)
    {
        if (string.IsNullOrWhiteSpace(view.Name) || view.Name.Trim().Length > 70)
            throw new ArgumentException("View name must be 1–70 characters.");
        await _gate.WaitAsync();
        try
        {
            var existing = _views.FindIndex(x=>x.Id==view.Id);
            view.SavedUtc=DateTimeOffset.UtcNow;
            if (existing>=0) _views[existing]=view;
            else
            {
                if (_views.Count >= MaxViews) throw new InvalidOperationException("Demo limit: 60 saved investigations.");
                _views.Add(view);
            }
            await SaveJsonAsync(_viewsFile,_views);
        }
        finally {_gate.Release();}
    }
    public async Task DeleteViewAsync(string id)
    {
        await _gate.WaitAsync();try {_views.RemoveAll(x=>x.Id==id);await SaveJsonAsync(_viewsFile,_views);}
        finally {_gate.Release();}
    }
}
