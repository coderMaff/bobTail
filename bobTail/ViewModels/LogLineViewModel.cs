using Avalonia.Media;
using ReactiveUI;

namespace bobTail.ViewModels;

public class LogLineViewModel : ViewModelBase
{
    private string _text = string.Empty;
    public string Text
    {
        get => _text;
        set => this.RaiseAndSetIfChanged(ref _text, value);
    }

    private IBrush _foreground = Brushes.White;
    public IBrush Foreground
    {
        get => _foreground;
        set => this.RaiseAndSetIfChanged(ref _foreground, value);
    }

    private IBrush _background = Brushes.Transparent;
    public IBrush Background
    {
        get => _background;
        set => this.RaiseAndSetIfChanged(ref _background, value);
    }

    public LogLineViewModel()
    {
    }

    public LogLineViewModel(string text)
    {
        Text = text;
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }

    private bool _isHidden;
    public bool IsHidden
    {
        get => _isHidden;
        set => this.RaiseAndSetIfChanged(ref _isHidden, value);
    }

}
