namespace NodalWorkflowLab.Models;

/// <summary>
/// Defines which repair visits appear in a selected node/edge slice.
/// A process being historically completed does not mean the engine is
/// currently at that process. The user can explicitly choose All eligible to see the full potential queue,
/// including entries with no observed step event.
/// </summary>
public static class FleetVisitSlicer
{
    public static bool IsCurrent(string status) => status is "In progress" or "Ready" or "Waiting" or "Blocked";

    public static bool MatchesStatus(string sourceStatus, string targetStatus,
        string mode, bool isHandoff)
    {
        return mode switch
        {
            "current" when isHandoff => sourceStatus == "Complete" && IsCurrent(targetStatus),
            "current" => IsCurrent(sourceStatus),
            "all" => true,
            "In progress" => sourceStatus is "In progress" or "Ready",
            _ => sourceStatus == mode
        };
    }
    public static bool MatchesProcesses(EngineCase engine, IEnumerable<string> processIds,
        string mode, ProcessMapSnapshot map)
    {
        var ids=processIds.Distinct(StringComparer.Ordinal).ToArray();
        if(ids.Length==0)return true;
        if(mode=="intersection")return ids.All(id=>IsCurrent(FleetAnalytics.Status(engine,map.Get(id),map)));
        return ids.Any(id=>IsCurrent(FleetAnalytics.Status(engine,map.Get(id),map)));
    }
}
