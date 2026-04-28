using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia.Media;
using bobTail.ViewModels;

namespace bobTail.Models;

public static class StateService
{
    private static string StatePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "bobTail",
            "settings.json");

    public static void SaveState(
        IEnumerable<LogTabViewModel> tabs,
        bool debugVisible,
        bool defaultTail,
        IEnumerable<HighlightRule> highlightRules,
        bool lineNumbersVisible = true)
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
            lineNumbersVisible,
            highlightRules = highlightRules.Select(r => new HighlightRuleDto
            {
                Text = r.Text,
                MatchMode = r.MatchMode,
                ForegroundColor = r.ForegroundColor.ToString(),
                BackgroundColor = r.BackgroundColor.ToString()
            }).ToList()
        };

        File.WriteAllText(StatePath, JsonSerializer.Serialize(state));
    }

    public static (List<string> files, bool debugVisible, bool defaultTail, bool lineNumbersVisible, List<HighlightRule> highlightRules)? LoadState()
    {




        if (!File.Exists(StatePath))
            return null;

        try
        {
            var json = File.ReadAllText(StatePath);
            var state = JsonSerializer.Deserialize<StateModel>(json);            

            var rules = state?.highlightRules?.Select(r => new HighlightRule
            {
                Text = r.Text,
                MatchMode = r.MatchMode,
                ForegroundColor = Color.Parse(r.ForegroundColor),
                BackgroundColor = Color.Parse(r.BackgroundColor)
            }).ToList() ?? new List<HighlightRule>();

            return (
                state!.openFiles,
                state.debugVisible,
                state.defaultTail,
                state.lineNumbersVisible,
                rules
            );
        }
        catch (Exception ex)
        {
            var debugPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"state_debug.txt");
            File.AppendAllText(debugPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR in LoadState()\n" +
                $"StatePath: {StatePath}\n" +
                $"Message: {ex.Message}\n" +
                $"Type: {ex.GetType().FullName}\n" +
                $"Stack:\n{ex.StackTrace}\n\n");
            // Return null if there's any deserialization error, will start fresh
            return null;
        }
    }

    private class HighlightRuleDto
    {
        public string Text { get; set; } = string.Empty;
        public string MatchMode { get; set; } = HighlightRule.Exact;
        public string ForegroundColor { get; set; } = "White";
        public string BackgroundColor { get; set; } = "Transparent";
    }

    private class StateModel
    {
        public List<string> openFiles { get; set; } = new();
        public bool debugVisible { get; set; }
        public bool defaultTail { get; set; } = true;
        public bool lineNumbersVisible { get; set; } = true;
        public ObservableCollection<HighlightRuleDto>? highlightRules { get; set; } = new();
    }
}
