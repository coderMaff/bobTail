using System.Collections.ObjectModel;
using Material.Icons;

namespace bobTail.ViewModels;

public class LogTabViewModel : ViewModelBase
{
    public string Title { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public bool IsDebug { get; set; }
    public MaterialIconKind IconKind { get; set; }

    public ObservableCollection<string> Lines { get; } = new();

    private long _readOffset = -1;
    public long ReadOffset
    {
        get => _readOffset;
        set => _readOffset = value;
    }

    public bool AutoScroll { get; set; } = true;
}
