using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using bobTail.ViewModels;

public static class StateService
{
    private static string StatePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "bobTail",
            "open_logs.json");

    public static void SaveState(IEnumerable<LogTabViewModel> tabs, bool debugVisible)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);

        var state = new
        {
            openFiles = tabs
                .Where(t => !t.IsDebug && t.FilePath != null)
                .Select(t => t.FilePath)
                .ToList(),
            debugVisible = debugVisible
        };

        File.WriteAllText(StatePath, JsonSerializer.Serialize(state));
    }

    public static (List<string> files, bool debugVisible)? LoadState()
    {
        if (!File.Exists(StatePath))
            return null;

        var json = File.ReadAllText(StatePath);
        var state = JsonSerializer.Deserialize<StateModel>(json);

        return (state!.openFiles, state.debugVisible);
    }

    private class StateModel
    {
        public List<string> openFiles { get; set; } = new();
        public bool debugVisible { get; set; }
    }
}
