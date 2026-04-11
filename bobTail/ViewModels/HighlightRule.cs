using Avalonia.Media;
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

    private Color _foregroundColor = Colors.White;
    public Color ForegroundColor
    {
        get => _foregroundColor;
        set => this.RaiseAndSetIfChanged(ref _foregroundColor, value);
    }

    public string ForegroundColorHex
    {
        get => _foregroundColor.ToString();
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                return;
            try
            {
                var color = Color.Parse(value);
                ForegroundColor = color;
            }
            catch
            {
                // Invalid color format, ignore
            }
        }
    }

    private Color _backgroundColor = Colors.Transparent;
    public Color BackgroundColor
    {
        get => _backgroundColor;
        set => this.RaiseAndSetIfChanged(ref _backgroundColor, value);
    }

    public string BackgroundColorHex
    {
        get => _backgroundColor.ToString();
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                return;
            try
            {
                var color = Color.Parse(value);
                BackgroundColor = color;
            }
            catch
            {
                // Invalid color format, ignore
            }
        }
    }
}
