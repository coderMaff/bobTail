using ReactiveUI;

namespace bobTail.ViewModels;

public class HighlightRule : ViewModelBase
{
    public const string Exact = "Exact";
    public const string IgnoreCase = "Ignore Case";
    public const string Regex = "Regex";

    private string _text = string.Empty;
    public string Text
    {
        get => _text;
        set => this.RaiseAndSetIfChanged(ref _text, value?.Trim() ?? string.Empty);
    }

    private string _matchMode = Exact;
    public string MatchMode
    {
        get => _matchMode;
        set => this.RaiseAndSetIfChanged(ref _matchMode, value?.Trim() ?? Exact);
    }

    private string _foregroundColor = "White";
    public string ForegroundColor
    {
        get => _foregroundColor;
        set => this.RaiseAndSetIfChanged(ref _foregroundColor, value);
    }

    private string _backgroundColor = "Transparent";
    public string BackgroundColor
    {
        get => _backgroundColor;
        set => this.RaiseAndSetIfChanged(ref _backgroundColor, value);
    }
}
