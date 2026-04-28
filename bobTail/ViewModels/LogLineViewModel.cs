using System;
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
        set
        {
            this.RaiseAndSetIfChanged(ref _isSelected, value);
            Console.WriteLine($"LogLineViewModel: IsSelected changed to {value} for line '{Text}'");
        }
        
    }

    private bool _isHidden;
    public bool IsHidden
    {
        get => _isHidden;
        set => this.RaiseAndSetIfChanged(ref _isHidden, value);
    }

    private bool _isSearchResult;
    public bool IsSearchResult
    {
        get => _isSearchResult;
        set => this.RaiseAndSetIfChanged(ref _isSearchResult, value);
    }

    private int _lineNumber;
    public int LineNumber
    {
        get => _lineNumber;
        set => this.RaiseAndSetIfChanged(ref _lineNumber, value);
    }

}
