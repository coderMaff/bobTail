using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Text;
using Avalonia;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using bobTail.Models;
using bobTail.ViewModels;

namespace bobTail.Views;

public partial class MainWindow : Window
{
    private readonly TailService _tailService = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Dictionary<string, int> _highlightDebugCounts = new();
    private static FileStream? _debugLogStream;
    private static StreamWriter? _debugLogWriter;
    private static bool _shouldQuitAfterLoad = false;
    private int _filesLoadingCount = 0;
    private ScrollViewer? _currentScrollViewer;

    private void EnsureScrollViewerFound()
    {
        if (_currentScrollViewer == null)
        {
            var listBox = this.FindDescendantOfType<ListBox>();
            if (listBox != null)
            {
                _currentScrollViewer = listBox.FindDescendantOfType<ScrollViewer>();
            }
        }
    }
    private LogTabViewModel? _currentTab;

    public MainWindow()
    {
        InitializeComponent();

        var args = Environment.GetCommandLineArgs();
        _shouldQuitAfterLoad = args.Contains("-quit", StringComparer.OrdinalIgnoreCase);

        InitializeDebugLog();

        var state = StateService.LoadState();
        if (state != null)
        {
            Vm.DebugTabVisible = state.Value.debugVisible;
            Vm.DefaultTailEnabled = state.Value.defaultTail;

            foreach (var rule in state.Value.highlightRules)
                Vm.HighlightRules.Add(rule);

            foreach (var file in state.Value.files)
            {
                _filesLoadingCount++;
                _ = CreateLogTabAsync(file);
            }
        }
        else
        {
            Vm.DebugTabVisible = true;
            Vm.DefaultTailEnabled = true;
        }

        SubscribeToHighlightRuleChanges();

        Vm.PropertyChanged += Vm_PropertyChanged;
        // Initialize current tab
        _currentTab = Vm.SelectedTab;
        if (_currentTab != null)
        {
            _currentTab.PropertyChanged += Tab_PropertyChanged;
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
        {
            var highlighted = CreateHighlightedLine(line, tab.FilePath);
            tab.Lines.Add(highlighted);
        }

        Vm.Tabs.Add(tab);
        Vm.SelectedTab = tab;

        // Scroll to bottom if AutoScroll is enabled
        if (tab.AutoScroll && tab.Lines.Count > 0)
        {
            _ = Task.Delay(100).ContinueWith(_ =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    EnsureScrollViewerFound();
                    ScrollToBottom();
                });
            });
        }

        AppendDebug($"[DEBUG] Opened file: {path}");
        AppendDebug($"[DEBUG] Initial size: {initialSize}");
        AppendDebug($"[DEBUG] Loaded {tab.Lines.Count} initial lines into tab");
        AppendDebug($"[DEBUG] Active highlight rules: {Vm.HighlightRules.Count}");
        foreach (var rule in Vm.HighlightRules)
            AppendDebug($"[DEBUG]   - {rule.Text} ({rule.MatchMode}) fg={rule.ForegroundColor:hsv} bg={rule.BackgroundColor:hsv}");

        _filesLoadingCount--;
        if (_shouldQuitAfterLoad && _filesLoadingCount == 0)
        {
            Dispatcher.UIThread.Post(async () =>
            {
                await Task.Delay(1000);
                AppendDebug("[DEBUG] -quit flag detected, closing after load complete.");
                FlushDebugLog();
                Environment.Exit(0);
            });
        }

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
                tab.Lines.Add(CreateHighlightedLine(captured, tab.FilePath));
                if (tab.Lines.Count > 2000)
                    tab.Lines.RemoveAt(0);

                if (Vm.SelectedTab != tab && !tab.AutoScroll)
                    tab.HasUnread = true;

                // Scroll to bottom if AutoScroll is enabled and this is the selected tab
                if (tab.AutoScroll && Vm.SelectedTab == tab)
                {
                    // Wait a frame for layout to measure the new item
                    _ = Task.Delay(10).ContinueWith(_ =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            EnsureScrollViewerFound();
                            ScrollToBottom();
                        }, DispatcherPriority.Input);
                    });
                }
            }, DispatcherPriority.Background);
        }
    }

    private void LogList_OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (Vm.SelectedTab is not LogTabViewModel tab)
            return;

        if (sender is not ScrollViewer sv)
            return;

        AppendDebug($"[DEBUG] Scroll changed: Offset={sv.Offset}, Viewport={sv.Viewport}, Extent={sv.Extent}");

        _currentScrollViewer = sv;

        //Only update AutoScroll based on position if we've already scrolled to bottom at least once
        if (tab.HasInitialScrolled)
        {
            tab.AutoScroll = sv.Offset.Y >= sv.Extent.Height - sv.Viewport.Height - 5;
        }
        else
        {
            tab.HasInitialScrolled = true;
        }

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
                tab.Lines.Insert(0, CreateHighlightedLine(line, tab.FilePath));
        });
    }

    private void ScrollToBottom()
    {
        AppendDebug("[DEBUG] ScrollToBottom called");
        if (_currentScrollViewer == null)
        {
            AppendDebug("[DEBUG] ScrollToBottom: _currentScrollViewer is null");
            return;
        }

        AppendDebug($"[DEBUG] Current extent: {_currentScrollViewer.Extent}, viewport: {_currentScrollViewer.Viewport}, offset: {_currentScrollViewer.Offset}");
        
        double targetOffset = Math.Max(0, _currentScrollViewer.Extent.Height - _currentScrollViewer.Viewport.Height);
        _currentScrollViewer.Offset = new Avalonia.Vector(0, targetOffset);
        
        AppendDebug($"[DEBUG] Scrolled to offset: {_currentScrollViewer.Offset}");
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedTab))
        {
            if (_currentTab != null)
            {
                _currentTab.PropertyChanged -= Tab_PropertyChanged;
            }
            _currentTab = Vm.SelectedTab;
            if (_currentTab != null)
            {
                _currentTab.PropertyChanged += Tab_PropertyChanged;
            }
            
            // Reset ScrollViewer reference when tab changes
            _currentScrollViewer = null;
            
            // If new tab has AutoScroll enabled, scroll to bottom
            if (_currentTab?.AutoScroll == true)
            {
                _ = Task.Delay(50).ContinueWith(_ =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        EnsureScrollViewerFound();
                        ScrollToBottom();
                    });
                });
            }
        }
    }

    private void Tab_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LogTabViewModel.AutoScroll) && _currentTab?.AutoScroll == true)
        {
            EnsureScrollViewerFound();
            Dispatcher.UIThread.Post(() => ScrollToBottom());
        }
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

    private void TopButton_OnClick(object? sender, RoutedEventArgs e)
    {
        AppendDebug("[DEBUG] Top button clicked");
        EnsureScrollViewerFound();
        
        if (_currentScrollViewer != null)
        {
            _currentScrollViewer.Offset = new Avalonia.Vector(0, 0);
            AppendDebug($"[DEBUG] Scrolled to top, offset: {_currentScrollViewer.Offset}");
        }
    }

    private void BottomButton_OnClick(object? sender, RoutedEventArgs e)
    {
        AppendDebug("[DEBUG] Bottom button clicked");
        EnsureScrollViewerFound();
        ScrollToBottom();
    }

    private void SettingsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var settings = new SettingsWindow
        {
            DataContext = DataContext,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        settings.Closed += (_, _) => RefreshAllHighlights();
        settings.Show(this);
        AppendDebug("[DEBUG] Settings window opened.");
    }

    private void SubscribeToHighlightRuleChanges()
    {
        Vm.HighlightRules.CollectionChanged += HighlightRules_CollectionChanged;
        foreach (var rule in Vm.HighlightRules)
            SubscribeHighlightRule(rule);
    }

    private void HighlightRules_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (HighlightRule rule in e.NewItems)
                SubscribeHighlightRule(rule);
        }

        if (e.OldItems != null)
        {
            foreach (HighlightRule rule in e.OldItems)
                rule.PropertyChanged -= HighlightRule_PropertyChanged;
        }

        RefreshAllHighlights();
    }

    private void SubscribeHighlightRule(HighlightRule rule)
    {
        rule.PropertyChanged += HighlightRule_PropertyChanged;
    }

    private void HighlightRule_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(RefreshAllHighlights);
    }

    private void RefreshAllHighlights()
    {
        foreach (var tab in Vm.Tabs)
        {
            foreach (var line in tab.Lines)
            {
                var matchRule = Vm.HighlightRules.FirstOrDefault(rule =>
                {
                    if (string.IsNullOrEmpty(rule.Text))
                        return false;

                    return rule.MatchMode switch
                    {
                        HighlightRule.Regex => TryRegexMatch(line.Text, rule.Text),
                        HighlightRule.IgnoreCase => line.Text.Contains(rule.Text, StringComparison.OrdinalIgnoreCase),
                        _ => line.Text.Contains(rule.Text, StringComparison.Ordinal)
                    };
                });

                if (matchRule != null)
                {
                    line.Foreground = ParseBrush(matchRule.ForegroundColor) ?? Brushes.White;
                    line.Background = ParseBrush(matchRule.BackgroundColor) ?? Brushes.Transparent;
                }
                else
                {
                    line.Foreground = Brushes.White;
                    line.Background = Brushes.Transparent;
                }
            }
        }
    }

    private void AboutButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var about = new AboutWindow
        {
            DataContext = DataContext,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        about.Show(this);
        AppendDebug("[DEBUG] about window opened.");
    }

    private void AppendDebug(string message)
    {
        Vm.DebugTab.Lines.Add(CreateHighlightedLine(message, null));
        _debugLogWriter?.WriteLine(message);
        _debugLogWriter?.Flush();
    }

    private static void InitializeDebugLog()
    {
        try
        {
            var debugLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.log");
            _debugLogStream = new FileStream(debugLogPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            _debugLogWriter = new StreamWriter(_debugLogStream, Encoding.UTF8);
            _debugLogWriter.WriteLine($"=== bobTail Debug Log ===");
            _debugLogWriter.WriteLine($"Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            _debugLogWriter.Flush();
        }
        catch
        {
            // Silently fail if we can't open log file
        }
    }

    private static void FlushDebugLog()
    {
        _debugLogWriter?.Flush();
        _debugLogWriter?.Close();
        _debugLogStream?.Close();
    }

    private LogLineViewModel CreateHighlightedLine(string line, string? filePath)
    {
        var logLine = new LogLineViewModel(line);
        HighlightRule? matchedRule = null;
        var evaluationResults = new List<string>();

        foreach (var rule in Vm.HighlightRules)
        {
            var textToMatch = rule.Text?.Trim();
            var matchMode = rule.MatchMode?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(textToMatch))
            {
                evaluationResults.Add($"{matchMode}:<empty>=N");
                continue;
            }

            var isMatch = matchMode.Equals(HighlightRule.Regex, StringComparison.OrdinalIgnoreCase)
                ? TryRegexMatch(line, textToMatch)
                : matchMode.Equals(HighlightRule.IgnoreCase, StringComparison.OrdinalIgnoreCase)
                    ? line.Contains(textToMatch, StringComparison.OrdinalIgnoreCase)
                    : line.Contains(textToMatch, StringComparison.Ordinal);

            evaluationResults.Add($"{matchMode}:{textToMatch}={(isMatch ? "Y" : "N")}");

            if (!isMatch)
                continue;

            matchedRule = rule;
            logLine.Foreground = ParseBrush(rule.ForegroundColor) ?? Brushes.White;
            logLine.Background = ParseBrush(rule.BackgroundColor) ?? Brushes.Transparent;
            break;
        }

        MaybeLogHighlightAnalysis(filePath, line, matchedRule, evaluationResults);
        return logLine;
    }

    private void MaybeLogHighlightAnalysis(string? filePath, string line, HighlightRule? matchedRule, List<string> evaluationResults)
    {
        if (filePath == null)
            return;

        if (!_highlightDebugCounts.TryGetValue(filePath, out var count))
            count = 0;

        if (count >= 10)
            return;

        _highlightDebugCounts[filePath] = count + 1;

        var preview = line.Length > 120 ? line.Substring(0, 120) + "..." : line;
        var lineNumber = count + 1;
        var ruleSummary = string.Join("; ", evaluationResults);

        if (matchedRule != null)
        {
            AppendDebug($"[HIGHLIGHT] {Path.GetFileName(filePath)} #{lineNumber}: '{preview}' -> '{matchedRule.Text}' ({matchedRule.MatchMode}) fg={matchedRule.ForegroundColor:hsv} bg={matchedRule.BackgroundColor:hsv} | rules={ruleSummary}");
        }
        else
        {
            AppendDebug($"[HIGHLIGHT] {Path.GetFileName(filePath)} #{lineNumber}: '{preview}' -> no match | rules={ruleSummary}");
        }
    }

    private static IBrush? ParseBrush(Color color)
    {
        return new SolidColorBrush(color);
    }

    private static bool TryRegexMatch(string line, string pattern)
    {
        try
        {
            return Regex.IsMatch(line, pattern);
        }
        catch
        {
            return false;
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        _cts.Cancel();
        FlushDebugLog();

        StateService.SaveState(
            Vm.Tabs,
            Vm.DebugTabVisible,
            Vm.DefaultTailEnabled,
            Vm.HighlightRules
        );
    }
}
