using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows.Threading;
using HelpDesk.Domain.Models;
using HelpDesk.Application.Interfaces;
using HelpDesk.Presentation.ViewModels;

namespace HelpDesk.Presentation.Views.Pages;

public sealed class FixCenterCategoryRailItem
{
    public string Title { get; init; } = "";
    public int Count { get; init; }
    public FixCategory? Category { get; init; }
    public string AutomationId { get; init; } = "";
}

public partial class FixCenterPage : Page
{
    private readonly MainViewModel _vm;
    private readonly IAppLogger _logger;
    private readonly ObservableCollection<FixCenterCategoryRailItem> _categoryRailItems = [];

    public FixCenterPage(MainViewModel vm, IAppLogger logger)
    {
        InitializeComponent();
        _vm = vm;
        _logger = logger;
        DataContext = _vm;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var stopwatch = Stopwatch.StartNew();
        BuildCategoryRail();
        _ = Dispatcher.InvokeAsync(() =>
        {
            FixSearchBox.Focus();
            _logger.Info($"Fix Center became interactive in {stopwatch.ElapsedMilliseconds} ms.");
        }, DispatcherPriority.Loaded);
    }

    private void BuildCategoryRail()
    {
        _categoryRailItems.Clear();
        _categoryRailItems.Add(new FixCenterCategoryRailItem
        {
            Title = "All Fixes",
            Count = _vm.TotalAccessibleFixCount,
            Category = null,
            AutomationId = "FixCenter_Category_AllFixes"
        });

        foreach (var category in _vm.Categories)
        {
            _categoryRailItems.Add(new FixCenterCategoryRailItem
            {
                Title = category.Title,
                Count = _vm.GetAccessibleFixCount(category),
                Category = category,
                AutomationId = $"FixCenter_Category_{ToPascalCase(category.Title)}"
            });
        }

        CategoryRailListBox.ItemsSource = _categoryRailItems;
        CategoryRailListBox.SelectedItem = _categoryRailItems.FirstOrDefault(item =>
            string.Equals(item.Category?.Id, _vm.SelectedCategory?.Id, StringComparison.OrdinalIgnoreCase))
            ?? _categoryRailItems.FirstOrDefault();
    }

    private static string ToPascalCase(string value)
    {
        var parts = value
            .Split([' ', '&', '/', '-', '.', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]);
        return string.Concat(parts);
    }

    private void CategoryRailListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CategoryRailListBox.SelectedItem is not FixCenterCategoryRailItem item)
            return;

        _vm.SelectFixCategory(item.Category);
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
        => _vm.SearchText = "";

    private async void FixButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: FixItem fix })
            await _vm.RunFixAsync(fix);
    }

    private async void FixMenuRun_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: FixItem fix })
            await _vm.RunFixAsync(fix);
    }

    private void FixMenuToggleDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: FixItem fix })
            _vm.ToggleFixExpansion(fix);
    }

    private void FixMenuCopyTitle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: FixItem fix })
            System.Windows.Clipboard.SetText(fix.Title);
    }

    private void AdvancedRiskHelp_Click(object sender, RoutedEventArgs e)
        => ShowHelpPopover(sender as FrameworkElement, "This fix makes significant changes to your PC. It's safe to run, but you should only use it if you're experiencing the specific problem it fixes.", "Help_AdvancedRisk_Popover");

    private void RequiresAdminHelp_Click(object sender, RoutedEventArgs e)
        => ShowHelpPopover(sender as FrameworkElement, "This fix needs administrator permission to run. If you're not an administrator on this PC, it won't work.", "Help_RequiresAdmin_Popover");

    private void FixCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: FixItem fix })
            return;

        if (e.OriginalSource is DependencyObject source && FindAncestor<System.Windows.Controls.Button>(source) is not null)
            return;

        _vm.ToggleFixExpansion(fix);
    }

    private void FixCard_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { ContextMenu: { } menu } border)
            return;

        border.Focus();
        menu.PlacementTarget = border;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
                return match;

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    public void FocusSearchBox() => FixSearchBox.Focus();

    public Task RunFocusedFixAsync()
    {
        var fix = GetFocusedFix();
        return fix is null ? Task.CompletedTask : _vm.RunFixAsync(fix);
    }

    public void ToggleFocusedFixExpansion()
    {
        var fix = GetFocusedFix();
        if (fix is not null)
            _vm.ToggleFixExpansion(fix);
    }

    private static FixItem? GetFocusedFix()
    {
        if (Keyboard.FocusedElement is FrameworkElement { DataContext: FixItem focusedFix })
            return focusedFix;

        if (Keyboard.FocusedElement is FrameworkElement focusedElement)
        {
            if (focusedElement.Tag is FixItem taggedFix)
                return taggedFix;

            var parent = FindAncestor<FrameworkElement>(focusedElement);
            while (parent is not null)
            {
                if (parent.DataContext is FixItem dataFix)
                    return dataFix;
                if (parent.Tag is FixItem parentFix)
                    return parentFix;
                parent = FindAncestor<FrameworkElement>(VisualTreeHelper.GetParent(parent));
            }
        }

        return null;
    }

    private static void ShowHelpPopover(FrameworkElement? anchor, string message, string automationId)
    {
        if (anchor is null)
            return;

        var menu = new ContextMenu
        {
            PlacementTarget = anchor,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
            StaysOpen = false
        };
        System.Windows.Automation.AutomationProperties.SetAutomationId(menu, automationId);
        menu.Items.Add(new MenuItem
        {
            Header = message,
            IsEnabled = false,
            MaxWidth = 320
        });
        menu.IsOpen = true;
    }
}
