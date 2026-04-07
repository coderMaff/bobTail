using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using bobTail.ViewModels;

namespace bobTail.Models;

public static class StateService
{
    private static string StatePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "bobTail",
            "open_logs.json");

    public static void SaveState(
        IEnumerable<LogTabViewModel> tabs,
        bool debugVisible,
        bool defaultTail,
        IEnumerable<HighlightRule> highlightRules)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);

        var state = new
        {
            openFiles = tabs
                .Where(t => !t.IsDebug && t.FilePath != null)
                .Select(t => t.FilePath!)
                .ToList(),
            debugVisible,
            defaultTail,
            highlightRules = highlightRules.ToList()
        };

        File.WriteAllText(StatePath, JsonSerializer.Serialize(state));
    }

    public static (List<string> files, bool debugVisible, bool defaultTail, List<HighlightRule> highlightRules)? LoadState()
    {
        if (!File.Exists(StatePath))
            return null;

        var json = File.ReadAllText(StatePath);
        var state = JsonSerializer.Deserialize<StateModel>(json);

        return (
            state!.openFiles,
            state.debugVisible,
            state.defaultTail,
            state.highlightRules ?? new List<HighlightRule>()
        );
    }

    private class StateModel
    {
        public List<string> openFiles { get; set; } = new();
        public bool debugVisible { get; set; }
        public bool defaultTail { get; set; } = true;
        public List<HighlightRule>? highlightRules { get; set; } = new();
    }
}
