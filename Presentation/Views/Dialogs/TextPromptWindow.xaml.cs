using System.Windows;

namespace HelpDesk.Presentation.Views.Dialogs;

public partial class TextPromptWindow : Window
{
    public TextPromptWindow(string title, string prompt, string confirmLabel = "Add")
    {
        InitializeComponent();
        Title = title;
        Prompt = prompt;
        ConfirmLabel = confirmLabel;
        DataContext = this;
        Loaded += (_, _) => ValueTextBox.Focus();
    }

    public string Prompt { get; }
    public string ConfirmLabel { get; }
    public string ResponseText => ValueTextBox.Text.Trim();

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
