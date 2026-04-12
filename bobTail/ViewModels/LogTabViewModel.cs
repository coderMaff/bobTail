using System;
using System.Collections.ObjectModel;
using Material.Icons;
using ReactiveUI;

namespace bobTail.ViewModels;

public class LogTabViewModel : ViewModelBase
{
    private string _title = string.Empty;
    public string Title
    {
        get => _title;
        set => this.RaiseAndSetIfChanged(ref _title, value);
    }

    public ObservableCollection<LogLineViewModel> Lines { get; } = new();

    private string? _filePath;
    public string? FilePath
    {
        get => _filePath;
        set => this.RaiseAndSetIfChanged(ref _filePath, value);
    }

    private MaterialIconKind _iconKind = MaterialIconKind.FileDocumentOutline;
    public MaterialIconKind IconKind
    {
        get => _iconKind;
        set => this.RaiseAndSetIfChanged(ref _iconKind, value);
    }

    private bool _isDebug;
    public bool IsDebug
    {
        get => _isDebug;
        set => this.RaiseAndSetIfChanged(ref _isDebug, value);
    }

    private bool _autoScroll = true;
    public bool AutoScroll
    {
        get => _autoScroll;
        set => this.RaiseAndSetIfChanged(ref _autoScroll, value);
    }

    public bool HasInitialScrolled { get; set; } = false;

    private long _readOffset;
    public long ReadOffset
    {
        get => _readOffset;
        set => this.RaiseAndSetIfChanged(ref _readOffset, value);
    }

    private bool _hasUnread;
    public bool HasUnread
    {
        get => _hasUnread;
        set => this.RaiseAndSetIfChanged(ref _hasUnread, value);
    }

    // -------------------------------
    // SCROLL REQUEST EVENT (correct)
    // -------------------------------
    public event Action<LogLineViewModel>? ScrollRequested;

    public void RequestScrollToLine(LogLineViewModel line)
    {
        ScrollRequested?.Invoke(line);
    }

    // -------------------------------
    // FILTERED LINES
    // -------------------------------
    private ObservableCollection<LogLineViewModel> _filteredLines = new();
    public ObservableCollection<LogLineViewModel> FilteredLines
    {
        get => _filteredLines;
        set => this.RaiseAndSetIfChanged(ref _filteredLines, value);
    }

    // -------------------------------
    // CONSTRUCTOR
    // -------------------------------
    public LogTabViewModel()
    {
        // Default: show all lines
        FilteredLines = Lines;
    }
}
