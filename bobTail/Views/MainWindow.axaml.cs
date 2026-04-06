using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
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
            foreach (var file in state.Value.files)
                _ = CreateLogTabAsync(file);

            Vm.DebugTabVisible = state.Value.debugVisible;
        }
        else
        {
            Vm.DebugTabVisible = true; // default during development
        }

    }

    private MainWindowViewModel Vm => (MainWindowViewModel)DataContext!;

    private async void OpenFileButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            AllowMultiple = false,
            Title = "Open log file"
        };

        var result = await dialog.ShowAsync(this);
        if (result == null || result.Length == 0)
            return;

        var path = result[0];
        if (!File.Exists(path))
        {
            AppendDebug($"[ERROR] File does not exist: {path}");
            return;
        }

        await CreateLogTabAsync(path);
    }

    private async Task CreateLogTabAsync(string path)
    {
        var tab = new LogTabViewModel
        {
            FilePath = path,
            Title = Path.GetFileName(path),
            IconKind = Material.Icons.MaterialIconKind.FileDocumentOutline,
            ReadOffset = new FileInfo(path).Length
        };

        Vm.Tabs.Add(tab);
        Vm.SelectedTab = tab;

        AppendDebug($"[DEBUG] Opened file: {path}");
        AppendDebug($"[DEBUG] Initial size: {tab.ReadOffset}");

        // Load last lines
        var lastLines = _tailService.LoadLastLines(path, 2000);
        foreach (var line in lastLines)
            tab.Lines.Add(line);

        // Start tailing
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

                if (tab.AutoScroll && Vm.SelectedTab == tab)
                {
                    // Find the ListBox in the current tab's visual tree
                    if (this.FindControl<TabControl>("") is { } tc &&
                        tc.SelectedContent is ListBox lb)
                    {
                        lb.ScrollIntoView(tab.Lines[^1]);
                    }
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
        if (sender is not Button btn) return;
        if (btn.CommandParameter is not LogTabViewModel tab) return;

        if (tab.IsDebug)
        {
            Vm.DebugTabVisible = false;
            return;
        }

        Vm.Tabs.Remove(tab);
    }


    private void SettingsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        AppendDebug("[DEBUG] Settings clicked (not implemented yet).");
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
            Vm.DebugTabVisible
        );
    }

}
