using System.Windows;
using System.Windows.Media;
using HelpDesk.Domain.Models;
using WBrush = System.Windows.Media.Brush;
using WBrushes = System.Windows.Media.Brushes;
using WColor = System.Windows.Media.Color;

namespace HelpDesk.Presentation.Views.Dialogs;

public partial class RunbookPostResultWindow : Window
{
    public RunbookDefinition Runbook { get; }
    public RunbookExecutionSummary Summary { get; }
    public IReadOnlyList<string> ChangeList { get; }
    public IReadOnlyList<RunbookPostResultStepRow> StepRows { get; }
    public string OutcomeLabel => Summary.Success ? "Success" : Summary.IsPartial ? "Partial" : "Failed";
    public WBrush OutcomeBackground => (WBrush)FindResource(Summary.Success ? "AccentGreenBrush" : Summary.IsPartial ? "FoxOrangeBrush" : "AccentRedBrush");
    public WBrush OutcomeForeground => WBrushes.White;
    public string NextStepHeading => Summary.Success ? "What to do next" : "What to do if the issue persists";
    public bool ShowEscalateButton => !Summary.Success;
    public bool SaveReceiptRequested { get; private set; }
    public bool EscalateRequested { get; private set; }

    public RunbookPostResultWindow(RunbookDefinition runbook, RunbookExecutionSummary summary)
    {
        InitializeComponent();
        Runbook = runbook;
        Summary = summary;
        ChangeList = summary.ChangesMade.Count == 0
            ? ["No concrete workflow changes were captured."]
            : summary.ChangesMade;
        StepRows = summary.StepResults
            .Select(step => new RunbookPostResultStepRow(
                step.Title,
                step.StatusLabel,
                step.Summary,
                step.Success
                    ? (WBrush)FindResource("AccentGreenBrush")
                    : (WBrush)FindResource("AccentRedBrush"),
                step.Success
                    ? new SolidColorBrush(WColor.FromArgb(32, 34, 197, 94))
                    : new SolidColorBrush(WColor.FromArgb(32, 220, 38, 38))))
            .ToList();
        DataContext = this;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
        => Close();

    private void SaveReceipt_Click(object sender, RoutedEventArgs e)
    {
        SaveReceiptRequested = true;
        Close();
    }

    private void Escalate_Click(object sender, RoutedEventArgs e)
    {
        EscalateRequested = true;
        Close();
    }
}

public sealed record RunbookPostResultStepRow(
    string Title,
    string StatusLabel,
    string Summary,
    WBrush StatusForeground,
    WBrush StatusBackground);
