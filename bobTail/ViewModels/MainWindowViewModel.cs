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
        set => this.RaiseAndSetIfChanged(ref _selectedTab, value);
    }

    public LogTabViewModel DebugTab { get; }

    public MainWindowViewModel()
    {
        DebugTab = new LogTabViewModel
        {
            Title = "Debug",
            IsDebug = true,
            IconKind = MaterialIconKind.BugOutline
        };

        Tabs.Add(DebugTab);
        SelectedTab = DebugTab;
    }
}
