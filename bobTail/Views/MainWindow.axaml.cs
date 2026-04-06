using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using bobTail.Models;
using bobTail.ViewModels;

namespace bobTail.Views;

public partial class MainWindow : Window
{
    private readonly TailService _tailService = new();
    private readonly CancellationTokenSource _cts = new();

    public MainWindow()
    {
        InitializeComponent();

        var state = StateService.LoadState();
        if (state != null)
        {
            Vm.DebugTabVisible = state.Value.debugVisible;
            Vm.DefaultTailEnabled = state.Value.defaultTail;

            foreach (var file in state.Value.files)
                _ = CreateLogTabAsync(file);
        }
        else
        {
            Vm.DebugTabVisible = true;
            Vm.DefaultTailEnabled = true;
        }
    }

    private MainWindowViewModel Vm => (MainWindowViewModel)DataContext!;

    private async void OpenFileButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (StorageProvider == null)
        {
            AppendDebug("[ERROR] StorageProvider not available.");
            return;
        }

        var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open log file",
            AllowMultiple = false
        });

        if (result == null || result.Count == 0)
            return;

        var file = result[0];
        var path = file.TryGetLocalPath();

        if (path == null)
        {
            AppendDebug("[ERROR] Unable to resolve local file path.");
            return;
        }

        if (!File.Exists(path))
        {
            AppendDebug($"[ERROR] File does not exist: {path}");
            return;
        }

        await CreateLogTabAsync(path);
    }

    private async Task CreateLogTabAsync(string path)
    {
        var info = new FileInfo(path);
        var initialSize = info.Exists ? info.Length : 0;

        var tab = new LogTabViewModel
        {
            FilePath = path,
            Title = Path.GetFileName(path),
            IconKind = Material.Icons.MaterialIconKind.FileDocumentOutline,
            ReadOffset = initialSize,
            AutoScroll = Vm.DefaultTailEnabled
        };

        // Load last lines
        var lastLines = _tailService.LoadLastLines(path, 2000).ToList();
        foreach (var line in lastLines)
            tab.Lines.Add(line);

        Vm.Tabs.Add(tab);
        Vm.SelectedTab = tab;

        AppendDebug($"[DEBUG] Opened file: {path}");
        AppendDebug($"[DEBUG] Initial size: {initialSize}");

        _ = Task.Run(() => TailLoopAsync(tab, _cts.Token));
    }

    private async Task TailLoopAsync(LogTabViewModel tab, CancellationToken token)
    {
        if (tab.FilePath == null)
            return;

        await foreach (var line in _tailService.TailFile(tab.FilePath, token))
        {
            var captured = line;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                tab.Lines.Add(captured);
                if (tab.Lines.Count > 2000)
                    tab.Lines.RemoveAt(0);

                if (Vm.SelectedTab != tab && !tab.AutoScroll)
                    tab.HasUnread = true;
            }, DispatcherPriority.Background);
        }
    }

    private void LogList_OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (Vm.SelectedTab is not LogTabViewModel tab)
            return;

        if (sender is not ScrollViewer sv)
            return;

        tab.AutoScroll = sv.Offset.Y >= sv.Extent.Height - sv.Viewport.Height - 5;

        if (tab.FilePath == null || tab.ReadOffset <= 0)
            return;

        if (sv.Offset.Y < 20)
            LoadOlderLines(tab);
    }

    private void LoadOlderLines(LogTabViewModel tab)
    {
        if (tab.FilePath == null)
            return;

        var offset = tab.ReadOffset;
        var older = _tailService.ReadPreviousChunk(tab.FilePath, ref offset).ToList();
        tab.ReadOffset = offset;

        if (older.Count == 0)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            foreach (var line in older)
                tab.Lines.Insert(0, line);
        });
    }

    private void CloseTab_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn)
            return;
        if (btn.CommandParameter is not LogTabViewModel tab)
            return;

        if (tab.IsDebug)
        {
            Vm.DebugTabVisible = false;
            return;
        }

        Vm.Tabs.Remove(tab);
    }

    private void SettingsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var settings = new SettingsWindow
        {
            DataContext = DataContext,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        settings.Show(this);
        AppendDebug("[DEBUG] Settings window opened.");
    }

    private void AppendDebug(string message)
    {
        Vm.DebugTab.Lines.Add(message);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        _cts.Cancel();

        StateService.SaveState(
            Vm.Tabs,
            Vm.DebugTabVisible,
            Vm.DefaultTailEnabled
        );
    }
}
