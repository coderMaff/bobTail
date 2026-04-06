using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using bobTail.Models;
using bobTail.ViewModels;

namespace bobTail.Views;

public partial class MainWindow : Window
{
    private readonly TailService _tailService = new();

    public MainWindow()
    {
        InitializeComponent();

        var state = StateService.LoadState();
        if (state != null)
        {
            Vm.DefaultTailEnabled = state.Value.defaultTail;
            Vm.DebugTabVisible = state.Value.debugVisible;

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
        if (this.StorageProvider == null)
        {
            AppendDebug("[ERROR] StorageProvider not available.");
            return;
        }

        var result = await this.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
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

        var tab = new LogTabViewModel
        {
            FilePath = path,
            Title = Path.GetFileName(path),
            IconKind = Material.Icons.MaterialIconKind.FileDocumentOutline,
            ReadOffset = info.Exists ? info.Length : 0,
            AutoScroll = Vm.DefaultTailEnabled
        };

        Vm.Tabs.Add(tab);
        Vm.SelectedTab = tab;

        AppendDebug($"[DEBUG] Opened file: {path}");
        AppendDebug($"[DEBUG] Initial size: {tab.ReadOffset}");

        var lastLines = _tailService.LoadLastLines(path, 2000);
        foreach (var line in lastLines)
            tab.Lines.Add(line);

        _ = Task.Run(() => TailLoopAsync(tab));
    }

    private async Task TailLoopAsync(LogTabViewModel tab)
    {
        if (tab.FilePath == null)
            return;

        await foreach (var line in _tailService.TailFile(tab.FilePath))
        {
            var captured = line;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                tab.Lines.Add(captured);
                if (tab.Lines.Count > 2000)
                    tab.Lines.RemoveAt(0);

                if (Vm.SelectedTab != tab && !tab.AutoScroll)
                {
                    tab.HasUnread = true;
                }

                if (tab.AutoScroll && Vm.SelectedTab == tab)
                {
                    // Auto-scroll handled by ListBox ScrollIntoView if needed
                    // You can wire this up via a behavior if you want it perfect
                }
            });
        }
    }

    private void LogList_OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (Vm.SelectedTab is not LogTabViewModel tab)
            return;

        if (sender is not ScrollViewer sv)
            return;

        tab.AutoScroll = sv.Offset.Y >= sv.Extent.Height - sv.Viewport.Height - 5;

        if (tab.FilePath == null || tab.ReadOffset < 0)
            return;

        if (sv.Offset.Y < 20)
            LoadOlderLines(tab);
    }

    private void LoadOlderLines(LogTabViewModel tab)
    {
        if (tab.FilePath == null)
            return;

        var offset = tab.ReadOffset;
        var older = _tailService.ReadPreviousChunk(tab.FilePath, ref offset);
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

        StateService.SaveState(
            Vm.Tabs,
            Vm.DebugTabVisible,
            Vm.DefaultTailEnabled
        );
    }
}
