using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Diagnostics;
using Microsoft.Win32;
using System.Windows.Threading;
using HelpDesk.Application.Interfaces;
using HelpDesk.Domain.Models;
using HelpDesk.Presentation.ViewModels;
using HelpDesk.Presentation.Views;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using NavPage = HelpDesk.Domain.Enums.Page;

namespace HelpDesk.Presentation.Views.Pages;

public partial class HistoryPage : Page
{
    private readonly MainViewModel _vm;
    private readonly IAppLogger _logger;

    public HistoryPage(MainViewModel vm, IAppLogger logger)
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
        _ = Dispatcher.InvokeAsync(() =>
        {
            HistorySearchBox.Focus();
            _logger.Info($"History page became interactive with {_vm.FilteredHistoryEntries.Count} visible receipts in {stopwatch.ElapsedMilliseconds} ms.");
        }, DispatcherPriority.ContextIdle);
    }

    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                $"Clear all recorded activity for {_vm.ProductDisplayName}? This removes repair, workflow, and automation receipts from the in-app history view.",
                $"{_vm.ProductDisplayName} - Clear Activity",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes)
            _vm.ClearHistory();
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
        => _vm.HistorySearchText = string.Empty;

    private void OpenRepairLibrary_Click(object sender, RoutedEventArgs e)
        => NavigateTo(NavPage.Fixes);

    private void OpenAutomation_Click(object sender, RoutedEventArgs e)
        => NavigateTo(NavPage.Bundles);

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
            _vm.SetAllVisibleHistorySelections(checkBox.IsChecked == true);
    }

    private void HistoryItemSelection_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { Tag: RepairHistoryEntry entry } checkBox)
            _vm.ToggleHistoryEntrySelection(entry, checkBox.IsChecked == true);
    }

    private void DeselectAll_Click(object sender, RoutedEventArgs e)
        => _vm.ClearHistorySelections();

    private void CloseCompare_Click(object sender, RoutedEventArgs e)
        => _vm.ClearHistorySelections();

    private void CompareSelected_Click(object sender, RoutedEventArgs e)
    {
        if (!_vm.OpenSelectedHistoryComparison())
        {
            MessageBox.Show(
                "Select exactly two receipts to compare them side by side.",
                $"{_vm.ProductDisplayName} - Compare Receipts",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private async void ExportSelected_Click(object sender, RoutedEventArgs e)
        => await ExportSelectedAsync();

    public async void ExportSelectedFromShortcut()
        => await ExportSelectedAsync();

    public void DeleteSelectedFromShortcut()
    {
        var selectedCount = _vm.SelectedHistoryEntryCount;
        if (selectedCount == 0)
            return;

        var confirmation = MessageBox.Show(
            $"Delete {selectedCount} selected receipt{(selectedCount == 1 ? string.Empty : "s")} from Activity? This only removes the saved receipt record.",
            $"{_vm.ProductDisplayName} - Delete Receipts",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.OK)
            return;

        _vm.DeleteSelectedHistoryEntries();
    }

    private async Task ExportSelectedAsync()
    {
        var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = ".json",
            Filter = "JSON files (*.json)|*.json",
            FileName = $"fixfox-receipts-{DateTime.Now:yyyyMMdd-HHmmss}.json",
            Title = "Export Selected Receipts"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            await _vm.ExportSelectedHistoryReceiptsAsync(dialog.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"FixFox could not export the selected receipts.\n\n{ex.Message}",
                $"{_vm.ProductDisplayName} - Export Receipts",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async void RerunFix_Click(object sender, RoutedEventArgs e)
    {
        var entry = sender switch
        {
            Button { Tag: RepairHistoryEntry buttonEntry } => buttonEntry,
            MenuItem { CommandParameter: RepairHistoryEntry menuEntry } => menuEntry,
            _ => null
        };

        if (entry is not null)
            await _vm.RerunHistoryEntryAsync(entry);
    }

    private void CopyDetails_Click(object sender, RoutedEventArgs e)
    {
        var entry = sender switch
        {
            Button { Tag: RepairHistoryEntry buttonEntry } => buttonEntry,
            MenuItem { CommandParameter: RepairHistoryEntry menuEntry } => menuEntry,
            _ => null
        };

        if (entry is null)
            return;

        System.Windows.Clipboard.SetText(_vm.BuildReceiptDetailText(entry));
    }

    private void ReceiptDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: RepairHistoryEntry entry })
            ShowReceiptDetails(entry);
    }

    private void ReceiptDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: RepairHistoryEntry entry })
            ShowReceiptDetails(entry);
    }

    private async void ViewRawReceipt_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: RepairHistoryEntry entry })
            return;

        try
        {
            var path = await _vm.WriteRawReceiptFileAsync(entry);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"FixFox could not open the raw receipt.\n\n{ex.Message}",
                $"{_vm.ProductDisplayName} - Raw Receipt",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async void CreateSupportPackage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _vm.CreateEvidenceBundleAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, $"{_vm.ProductDisplayName} - Support Package", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void HistoryCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || sender is not Border { Tag: RepairHistoryEntry entry })
            return;

        if (string.IsNullOrWhiteSpace(entry.FixId) && string.IsNullOrWhiteSpace(entry.RunbookId))
        {
            ShowReceiptDetails(entry);
            return;
        }

        await _vm.RerunHistoryEntryAsync(entry);
    }

    private void ShowReceiptDetails(RepairHistoryEntry entry)
    {
        MessageBox.Show(
            _vm.BuildReceiptDetailText(entry),
            $"{_vm.ProductDisplayName} - Receipt Details",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ReceiptHelp_Click(object sender, RoutedEventArgs e)
        => ShowHelpPopover(
            sender as FrameworkElement,
            "A record of what FixFox did and what changed. You can refer to this if a problem comes back.",
            "Help_Receipt_Popover");

    private void NavigateTo(NavPage page)
    {
        var shell = Window.GetWindow(this) as MainWindow
            ?? System.Windows.Application.Current?.MainWindow as MainWindow;
        shell?.NavigateToPage(page);
    }

    private void HistoryScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalChange <= 0)
            return;

        if (e.VerticalOffset + e.ViewportHeight < e.ExtentHeight - 48)
            return;

        var added = _vm.LoadMoreHistoryEntries();
        if (added > 0)
            _logger.Info($"History load-more appended {added} receipt(s); {_vm.FilteredHistoryEntries.Count} visible.");
    }

    private static void ShowHelpPopover(FrameworkElement? anchor, string message, string automationId)
    {
        if (anchor is null)
            return;

        var menu = new ContextMenu
        {
            PlacementTarget = anchor,
            StaysOpen = false
        };
        System.Windows.Automation.AutomationProperties.SetAutomationId(menu, automationId);
        menu.Items.Add(new MenuItem
        {
            Header = message,
            IsEnabled = false
        });
        menu.IsOpen = true;
    }
}
