using Avalonia.Controls;
using Avalonia.Interactivity;
using bobTail.ViewModels;

namespace bobTail.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void AddHighlightRule_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var rule = new HighlightRule();
        vm.HighlightRules.Add(rule);
        vm.SelectedHighlightRule = rule;
    }

    private void RemoveHighlightRule_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || vm.SelectedHighlightRule == null)
            return;

        vm.HighlightRules.Remove(vm.SelectedHighlightRule);
        vm.SelectedHighlightRule = null;
    }

    private void MoveHighlightRuleUp_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || vm.SelectedHighlightRule == null)
            return;

        var index = vm.HighlightRules.IndexOf(vm.SelectedHighlightRule);
        if (index <= 0)
            return;

        vm.HighlightRules.Move(index, index - 1);
    }

    private void MoveHighlightRuleDown_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || vm.SelectedHighlightRule == null)
            return;

        var index = vm.HighlightRules.IndexOf(vm.SelectedHighlightRule);
        if (index < 0 || index >= vm.HighlightRules.Count - 1)
            return;

        vm.HighlightRules.Move(index, index + 1);
    }
}
