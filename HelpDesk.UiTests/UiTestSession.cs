using System.Diagnostics;
using System.Runtime.InteropServices;
using FlaUI.Core.Input;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;
using Newtonsoft.Json;

namespace HelpDesk.UiTests;

internal sealed class UiTestSession : IDisposable
{
    private readonly string _profileRoot;
    private readonly bool _keepProfile;
    private bool _disposed;

    public UiTestSession(string profileRoot, bool keepProfile)
    {
        _profileRoot = profileRoot;
        _keepProfile = keepProfile;
    }

    public FlaUI.Core.Application Application { get; private set; } = null!;
    public UIA3Automation Automation { get; private set; } = null!;
    public Window MainWindow { get; private set; } = null!;
    public string ProfileRoot => _profileRoot;
    public string SettingsPath => Path.Combine(_profileRoot, "settings.json");

    public static UiTestSession Launch(bool seedCompletedSetup, string? existingProfileRoot = null)
    {
        var repoRoot = FindRepoRoot();
        var executablePath = Path.Combine(repoRoot, "bin", "Release", "net8.0-windows", "FixFox.exe");
        if (!File.Exists(executablePath))
            throw new FileNotFoundException($"UI automation expected the Release app at {executablePath}. Build the Release app first.", executablePath);

        KillLeakedTestProcesses(executablePath);

        var profileRoot = existingProfileRoot ?? Path.Combine(Path.GetTempPath(), "FixFox.UiTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(profileRoot);

        if (seedCompletedSetup)
            SeedCompletedSetup(profileRoot);

        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? repoRoot
        };
        startInfo.Environment["FIXFOX_APPDATA_DIR"] = profileRoot;

        var session = new UiTestSession(profileRoot, keepProfile: existingProfileRoot is null);
        session.Application = FlaUI.Core.Application.Launch(startInfo);
        session.Automation = new UIA3Automation();
        session.MainWindow = session.WaitForMainWindow();
        session.MainWindow.Focus();
        session.CloseModalIfPresent("FixFox");
        return session;
    }

    private static void KillLeakedTestProcesses(string executablePath)
    {
        foreach (var process in Process.GetProcessesByName("FixFox"))
        {
            try
            {
                if (!string.Equals(process.MainModule?.FileName, executablePath, StringComparison.OrdinalIgnoreCase))
                    continue;

                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
            catch
            {
                // Best-effort cleanup for leaked UI-test instances.
            }
        }
    }

    public ToggleButton FindToggle(string automationId)
        => WaitForElement(() => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId))?.AsToggleButton(),
            $"toggle {automationId}");

    public TextBox FindTextBox(string automationId)
        => WaitForElement(() => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId))?.AsTextBox(),
            $"textbox {automationId}");

    public Button FindButton(string automationId)
        => WaitForElement(() => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId))?.AsButton(),
            $"button {automationId}");

    public Window WaitForMainWindow()
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var window = Application.GetMainWindow(Automation, TimeSpan.FromSeconds(2));
                if (window is not null)
                    return window;
            }
            catch
            {
                // Retry until the real shell is ready.
            }

            Thread.Sleep(250);
        }

        throw new TimeoutException("FixFox did not surface a usable main window within 60 seconds.");
    }

    public void WaitForPage(string markerAutomationId, int timeoutSeconds = 40)
    {
        WaitForElement(
            () => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId(markerAutomationId)),
            $"page marker {markerAutomationId}",
            timeoutSeconds);
    }

    public void ClickNav(string automationId, string pageMarkerAutomationId)
    {
        Click(FindButton(automationId));
        WaitForPage(pageMarkerAutomationId);
    }

    public void ClickButton(string automationId)
        => Click(FindButton(automationId));

    public void WaitForControl(string automationId, int timeoutSeconds = 30)
    {
        WaitForElement(
            () => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
            $"control {automationId}",
            timeoutSeconds);
    }

    public void OpenCommandPalette()
    {
        MainWindow.Focus();
        FlaUI.Core.Input.Keyboard.TypeSimultaneously(
            FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
            FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_K);
        FindTextBox("CommandPaletteBox").Focus();
    }

    public void OpenPageWithShortcut(int digit, string pageMarkerAutomationId)
    {
        if (digit < 1 || digit > 9)
            throw new ArgumentOutOfRangeException(nameof(digit));

        MainWindow.Focus();
        FlaUI.Core.Input.Keyboard.TypeSimultaneously(
            FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
            (FlaUI.Core.WindowsAPI.VirtualKeyShort)(0x30 + digit));
        WaitForPage(pageMarkerAutomationId);
    }

    public void CloseCommandPalette()
    {
        MainWindow.Focus();
        FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
    }

    public void CloseModalIfPresent(string titleContains, string buttonText = "OK", int timeoutSeconds = 5)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            Window? modal = null;
            try
            {
                modal = Automation.GetDesktop()
                    .FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window))
                    .Select(child => child.AsWindow())
                    .FirstOrDefault(window =>
                        window.Properties.ProcessId.ValueOrDefault == Application.ProcessId
                        && window.Title.Contains(titleContains, StringComparison.OrdinalIgnoreCase)
                        && window.Title != MainWindow.Title);
            }
            catch (COMException)
            {
                Thread.Sleep(150);
                continue;
            }
            catch (InvalidOperationException)
            {
                Thread.Sleep(150);
                continue;
            }

            if (modal is null)
            {
                Thread.Sleep(150);
                continue;
            }

            var button = modal.FindFirstDescendant(cf => cf.ByText(buttonText))?.AsButton()
                ?? modal.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button))?.AsButton();
            if (button is not null)
                Click(button);
            Thread.Sleep(250);
            return;
        }
    }

    public static void Click(AutomationElement element)
    {
        try
        {
            element.Focus();
        }
        catch
        {
            // Focus can fail when WPF has just rebuilt the visual tree; continue with invoke/click.
        }

        if (element.Patterns.Invoke.IsSupported)
        {
            element.Patterns.Invoke.Pattern.Invoke();
            return;
        }

        if (element.Patterns.SelectionItem.IsSupported)
        {
            element.Patterns.SelectionItem.Pattern.Select();
            return;
        }

        try
        {
            var point = element.GetClickablePoint();
            Mouse.MoveTo(point);
            Mouse.Click(MouseButton.Left);
            return;
        }
        catch
        {
            // Fall through to a clearer failure.
        }

        throw new InvalidOperationException($"Element '{element.Name}' could not be clicked.");
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            if (Application?.HasExited == false)
                Application.Close();
        }
        catch
        {
        }

        try
        {
            var process = Process.GetProcessById(Application!.ProcessId);
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
        }
        catch
        {
            // Ignore cleanup failures.
        }

        try
        {
            Automation?.Dispose();
        }
        catch
        {
            // Best effort during test cleanup.
        }

        if (_keepProfile)
        {
            try
            {
                Directory.Delete(_profileRoot, recursive: true);
            }
            catch
            {
                // Keep the profile around if Windows still has a file handle.
            }
        }
    }

    public static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "HelpDesk.csproj")))
                return current;

            current = Directory.GetParent(current)?.FullName ?? string.Empty;
        }

        throw new DirectoryNotFoundException("Could not locate the FixFox repository root from the UI test output directory.");
    }

    public static void SeedCompletedSetup(string profileRoot)
    {
        Directory.CreateDirectory(profileRoot);

        var settings = new AppSettings
        {
            Theme = "Dark",
            Accent = "Orange",
            RunQuickScanOnLaunch = false,
            CheckForUpdatesOnLaunch = false,
            MinimizeToTray = false,
            RunAtStartup = false,
            ShowTrayBalloons = false,
            RunFirstHealthCheckAfterSetup = false,
            OnboardingDismissed = true,
            PrivacyNoticeDismissed = true,
            LastSessionEndedCleanly = true,
            BehaviorProfile = "Standard",
            DefaultLandingPage = Domain.Enums.Page.Dashboard.ToString(),
            Edition = AppEdition.Basic
        };

        File.WriteAllText(
            Path.Combine(profileRoot, "settings.json"),
            JsonConvert.SerializeObject(settings, Formatting.Indented));
    }

    private T WaitForElement<T>(Func<T?> lookup, string description, int timeoutSeconds = 15)
        where T : class
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var element = lookup();
                if (element is not null)
                    return element;
            }
            catch (COMException)
            {
                // WPF can briefly hand UIA a stale tree during navigation; retry.
            }
            catch (InvalidOperationException)
            {
                // Retry while the visual tree is being rebuilt.
            }

            Thread.Sleep(200);
        }

        throw new TimeoutException($"Timed out waiting for {description}.{Environment.NewLine}{BuildWindowDiagnosticSnapshot()}");
    }

    private string BuildWindowDiagnosticSnapshot()
    {
        try
        {
            var descendants = MainWindow.FindAllDescendants();
            var interesting = descendants
                .Select(element => new
                {
                    Id = element.AutomationId,
                    Name = element.Name,
                    ControlType = element.ControlType.ToString()
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.Id) || !string.IsNullOrWhiteSpace(item.Name))
                .Take(80)
                .Select(item => $"{item.ControlType} | Id='{item.Id}' | Name='{item.Name}'");

            return "UI snapshot:" + Environment.NewLine + string.Join(Environment.NewLine, interesting);
        }
        catch (Exception ex)
        {
            return $"UI snapshot unavailable: {ex.GetType().Name}: {ex.Message}";
        }
    }
}
