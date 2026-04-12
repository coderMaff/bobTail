using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using Material.Icons;
using ReactiveUI;

namespace bobTail.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public ObservableCollection<LogTabViewModel> Tabs { get; } = new();
    public ObservableCollection<HighlightRule> HighlightRules { get; } = new();
    public ReactiveCommand<Unit, Unit> FindNextCommand { get; }
    public ReactiveCommand<Unit, Unit> FindPrevCommand { get; }


    public string[] MatchModes { get; } =
    {
        HighlightRule.Exact,
        HighlightRule.IgnoreCase,
        HighlightRule.Regex
    };

    private HighlightRule? _selectedHighlightRule;
    public HighlightRule? SelectedHighlightRule
    {
        get => _selectedHighlightRule;
        set => this.RaiseAndSetIfChanged(ref _selectedHighlightRule, value);
    }

    private LogTabViewModel? _selectedTab;
    public LogTabViewModel? SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (value != _selectedTab)
            {
                this.RaiseAndSetIfChanged(ref _selectedTab, value);
                if (value != null)
                    value.HasUnread = false;
            }
        }
    }

    public LogTabViewModel DebugTab { get; }

    public MainWindowViewModel()
    {
        DebugTab = new LogTabViewModel
        {
            Title = "Debug",
            IsDebug = true,
            IconKind = MaterialIconKind.BugOutline,
            AutoScroll = true
        };

        Tabs.Add(DebugTab);
        SelectedTab = DebugTab;
    }

    private bool _debugTabVisible = true;
    public bool DebugTabVisible
    {
        get => _debugTabVisible;
        set
        {
            this.RaiseAndSetIfChanged(ref _debugTabVisible, value);
            UpdateDebugTabVisibility();
        }
    }

    private bool _defaultTailEnabled = true;
    public bool DefaultTailEnabled
    {
        get => _defaultTailEnabled;
        set => this.RaiseAndSetIfChanged(ref _defaultTailEnabled, value);
    }

    private void UpdateDebugTabVisibility()
    {
        if (DebugTabVisible)
        {
            if (!Tabs.Contains(DebugTab))
                Tabs.Insert(0, DebugTab);
        }
        else
        {
            if (Tabs.Contains(DebugTab))
                Tabs.Remove(DebugTab);
        }
    }

    private string? _findText;
    public string? FindText
    {
        get => _findText;
        set => this.RaiseAndSetIfChanged(ref _findText, value);
    }

    private string? _filterText;
    public string? FilterText
    {
        get => _filterText;
        set => this.RaiseAndSetIfChanged(ref _filterText, value);
    }

    public ReactiveCommand<Unit, Unit> FilterCommand { get; }

    private bool _isFilterActive;
    public bool IsFilterActive
    {
        get => _isFilterActive;
        set => this.RaiseAndSetIfChanged(ref _isFilterActive, value);
    }




    private void FilterLines()
    {
        if (SelectedTab == null)
            return;

        var searchText = FindText?.Trim();
        if (string.IsNullOrWhiteSpace(searchText))
            return;

        IsFilterActive = !IsFilterActive;

        if (IsFilterActive)
        {
            SelectedTab.FilteredLines = new ObservableCollection<LogLineViewModel>(
                SelectedTab.Lines.Where(line =>
                    line.Text.Contains(searchText, StringComparison.OrdinalIgnoreCase)));
        }
        else
        {
            SelectedTab.FilteredLines = new ObservableCollection<LogLineViewModel>(SelectedTab.Lines);
        }
    }


}
