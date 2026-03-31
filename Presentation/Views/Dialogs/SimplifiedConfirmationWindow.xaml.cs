using System.Windows;
using HelpDesk.Domain.Enums;

namespace HelpDesk.Presentation.Views.Dialogs;

public partial class SimplifiedConfirmationWindow : Window
{
    public SimplifiedConfirmationWindow(
        string title,
        string message,
        string primaryLabel,
        string secondaryLabel,
        bool showHelpAction = false,
        string helpLabel = "Get help instead")
    {
        InitializeComponent();
        DialogTitle = title;
        Message = message;
        PrimaryLabel = primaryLabel;
        SecondaryLabel = secondaryLabel;
        ShowHelpAction = showHelpAction;
        HelpLabel = helpLabel;
        DataContext = this;
    }

    public string DialogTitle { get; }
    public string Message { get; }
    public string PrimaryLabel { get; }
    public string SecondaryLabel { get; }
    public bool ShowHelpAction { get; }
    public string HelpLabel { get; }
    public SimplifiedConfirmationDecision Decision { get; private set; } = SimplifiedConfirmationDecision.Cancel;

    private void Run_Click(object sender, RoutedEventArgs e)
    {
        Decision = SimplifiedConfirmationDecision.Run;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Decision = SimplifiedConfirmationDecision.Cancel;
        DialogResult = false;
        Close();
    }

    private void Help_Click(object sender, RoutedEventArgs e)
    {
        Decision = SimplifiedConfirmationDecision.GetHelpInstead;
        DialogResult = false;
        Close();
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            Decision = SimplifiedConfirmationDecision.Cancel;
            Close();
        }
    }
}
