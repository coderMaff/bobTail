using System.Collections.ObjectModel;
using Material.Icons;
using ReactiveUI;

namespace bobTail.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public ObservableCollection<LogTabViewModel> Tabs { get; } = new();

    private LogTabViewModel? _selectedTab;
    public LogTabViewModel? SelectedTab
    {
        get => _selectedTab;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedTab, value);
            if (value != null)
                value.HasUnread = false;
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
}
