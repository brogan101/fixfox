using System.Windows;
using HelpDesk.Domain.Models;

namespace HelpDesk.Presentation.Views.Dialogs;

public partial class RunbookPreflightWindow : Window
{
    public RunbookDefinition Runbook { get; }
    public IReadOnlyList<string> StepTitles { get; }
    public IReadOnlyList<string> IrreversibleItems { get; }
    public bool HasIrreversibleActions => Runbook.IrreversibleActions.Count > 0;

    public RunbookPreflightWindow(RunbookDefinition runbook)
    {
        InitializeComponent();
        Runbook = runbook;
        StepTitles = runbook.Steps.Select(step => step.Title).ToList();
        IrreversibleItems = runbook.IrreversibleActionsOrDefault.ToList();
        DataContext = this;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
        => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e)
        => DialogResult = false;
}
