using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using HelpDesk.Domain.Models;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace HelpDesk.UiTests;

[Collection("UI")]
public sealed class FixFoxSmokeUiTests
{
    private readonly ITestOutputHelper _output;

    public FixFoxSmokeUiTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [StaFact]
    public void Fresh_Profile_Launches_And_Leaves_Onboarding_Pending_When_Not_Completed()
    {
        using var session = UiTestSession.Launch(seedCompletedSetup: false);

        session.WaitForPage("DashboardPageMarker");

        var settings = JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(session.SettingsPath));
        Assert.NotNull(settings);
        Assert.False(settings!.OnboardingDismissed);
    }

    [StaFact]
    public void Shell_DeviceHealth_Toolbox_And_Automation_Flows_Work_Through_The_Real_App()
    {
        using var session = UiTestSession.Launch(seedCompletedSetup: true);

        session.WaitForPage("DashboardPageMarker");
        Assert.Single(
            session.Automation.GetDesktop().FindAllChildren(cf => cf.ByControlType(ControlType.Window)),
            child => child.Properties.ProcessId.IsSupported
                && child.Properties.ProcessId.ValueOrDefault == session.Application.ProcessId);

        session.ClickButton("DashboardOpenDeviceHealthButton");
        session.WaitForPage("SystemInfoPageMarker");
        WaitUntil(() => session.FindButton("SystemInfoRefreshSnapshotButton").IsEnabled, 20,
            "device snapshot refresh to become available");
        session.ClickButton("SystemInfoRefreshSnapshotButton");

        session.ClickNav("NavToolbox", "ToolboxPageMarker");

        session.ClickNav("NavBundles", "BundlesPageMarker");
        session.WaitForControl("AutomationRunQuickHealthButton");
        session.ClickButton("AutomationRunQuickHealthButton");
        WaitUntil(() => session.MainWindow.FindFirstDescendant(cf => cf.ByText("Automation history")) is not null, 15,
            "automation page to remain responsive after quick health");
        WaitUntil(() => session.MainWindow.FindFirstDescendant(cf => cf.ByText("Scheduled Quick Health Check")) is not null, 60,
            "automation history entry for quick health");

        session.ClickButton("AutomationReceiptDetailsButton");
        session.CloseModalIfPresent("Automation Result");

    }

    [StaFact]
    public void Fixes_Diagnosis_And_History_Empty_State_Work()
    {
        using var session = UiTestSession.Launch(seedCompletedSetup: true);

        session.WaitForPage("DashboardPageMarker");
        session.ClickNav("NavHistory", "HistoryPageMarker");
        var emptyStateRepairLibraryButton = WaitUntilElement(
            () => session.MainWindow.FindAllDescendants(cf => cf.ByText("Open Repair Library"))
                .FirstOrDefault()?.AsButton(),
            10,
            "history empty-state repair library button");
        UiTestSession.Click(emptyStateRepairLibraryButton);
        session.WaitForPage("FixCenterPageMarker");

        var fixSearchBox = session.FindTextBox("FixCenterSearchBox");
        fixSearchBox.Text = "network";
        session.ClickButton("FixCenterDetailsButton");
        session.CloseModalIfPresent("Repair Details");

        session.ClickNav("NavSymptomChecker", "SymptomCheckerPageMarker");
        var symptomInput = session.FindTextBox("SymptomInputBox");
        symptomInput.Text = "browser issue";
        session.ClickButton("SymptomAnalyzeButton");
        session.ClickButton("SymptomResultDetailsButton");
        session.CloseModalIfPresent("Repair Details");
    }

    [StaFact]
    public void Settings_And_Command_Palette_Work()
    {
        using var session = UiTestSession.Launch(seedCompletedSetup: true);

        session.WaitForPage("DashboardPageMarker");
        UiTestSession.Click(session.FindButton("NavSettings"));
        session.WaitForControl("SettingsShowNotificationsToggle");
        var notificationsToggle = session.FindToggle("SettingsShowNotificationsToggle");
        var previousToggleState = notificationsToggle.ToggleState;
        notificationsToggle.Toggle();
        WaitUntil(() => session.FindToggle("SettingsShowNotificationsToggle").ToggleState != previousToggleState, 5,
            "notification toggle to change");
        session.ClickButton("SettingsOpenAutomationCenterButton");
        session.WaitForControl("AutomationRunQuickHealthButton");

        session.ClickButton("OpenCommandPaletteButton");
        var commandPalette = session.FindTextBox("CommandPaletteBox");
        commandPalette.Text = "automation";
        session.ClickButton("automation-open-center");
        session.WaitForControl("AutomationRunQuickHealthButton");
    }

    [StaFact]
    public void Support_Package_Flow_Creates_Local_Evidence()
    {
        using var session = UiTestSession.Launch(seedCompletedSetup: true);

        session.WaitForPage("DashboardPageMarker");
        session.ClickNav("NavHandoff", "HandoffPageMarker");
        session.FindButton("HandoffCreateSupportPackageButton").Invoke();
        var evidenceRoot = Path.Combine(session.ProfileRoot, "evidence-bundles");
        WaitUntil(() => Directory.Exists(evidenceRoot) && Directory.GetDirectories(evidenceRoot).Any(), 90,
            "support package files to be created");
        session.CloseModalIfPresent("Support Package");
    }

    [StaFact]
    public void Settings_Changes_Persist_Across_Relaunch()
    {
        var profileRoot = Path.Combine(Path.GetTempPath(), "FixFox.UiTests", Guid.NewGuid().ToString("N"));
        UiTestSession.SeedCompletedSetup(profileRoot);

        ToggleState expectedState;
        using (var session = UiTestSession.Launch(seedCompletedSetup: false, existingProfileRoot: profileRoot))
        {
            session.WaitForPage("DashboardPageMarker");
            UiTestSession.Click(session.FindButton("NavSettings"));
            session.WaitForControl("SettingsShowNotificationsToggle");
            var notificationsToggle = session.FindToggle("SettingsShowNotificationsToggle");
            expectedState = notificationsToggle.ToggleState == ToggleState.On ? ToggleState.Off : ToggleState.On;
            notificationsToggle.Toggle();
            WaitUntil(() => session.FindToggle("SettingsShowNotificationsToggle").ToggleState == expectedState, 5,
                "show-notifications toggle to persist in memory");
        }

        using (var secondSession = UiTestSession.Launch(seedCompletedSetup: false, existingProfileRoot: profileRoot))
        {
            secondSession.WaitForPage("DashboardPageMarker");
            UiTestSession.Click(secondSession.FindButton("NavSettings"));
            secondSession.WaitForControl("SettingsShowNotificationsToggle");
            Assert.Equal(expectedState, secondSession.FindToggle("SettingsShowNotificationsToggle").ToggleState);
        }

        try
        {
            Directory.Delete(profileRoot, recursive: true);
        }
        catch
        {
            _output.WriteLine($"UI test profile was left behind at {profileRoot}");
        }
    }

    private static void OpenContextMenuAndChoose(UiTestSession session, string anchorText, string menuItemText)
    {
        var anchor = WaitUntilElement(
            () => session.MainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                .FirstOrDefault(element => string.Equals(element.Name, anchorText, StringComparison.OrdinalIgnoreCase)),
            15,
            $"{anchorText} anchor text");

        var candidate = anchor;
        for (var depth = 0; depth < 5 && candidate is not null; depth++)
        {
            try
            {
                var point = candidate.GetClickablePoint();
                Mouse.MoveTo(point);
                Mouse.Click(MouseButton.Right);

                var menuItem = TryFindMenuItem(session, menuItemText, 2);
                if (menuItem is not null)
                {
                    menuItem.Invoke();
                    return;
                }
            }
            catch
            {
                // Try a broader ancestor surface next.
            }

            candidate = candidate.Parent;
        }

        throw new TimeoutException($"Timed out waiting for context menu item {menuItemText}.");
    }

    private static void OpenContextMenuAndChooseByAutomationId(UiTestSession session, string automationId, string menuItemText)
    {
        var anchor = WaitUntilElement(
            () => session.MainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
            15,
            $"{automationId} anchor");

        var candidate = anchor;
        for (var depth = 0; depth < 5 && candidate is not null; depth++)
        {
            try
            {
                var point = candidate.GetClickablePoint();
                Mouse.MoveTo(point);
                Mouse.Click(MouseButton.Right);

                var menuItem = TryFindMenuItem(session, menuItemText, 2);
                if (menuItem is not null)
                {
                    menuItem.Invoke();
                    return;
                }
            }
            catch
            {
            }

            candidate = candidate.Parent;
        }

        throw new TimeoutException($"Timed out waiting for context menu item {menuItemText}.");
    }

    private static MenuItem? TryFindMenuItem(UiTestSession session, string menuItemText, int timeoutSeconds)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var menuItem = session.Automation.GetDesktop().FindFirstDescendant(cf => cf.ByText(menuItemText))?.AsMenuItem();
            if (menuItem is not null)
                return menuItem;

            Thread.Sleep(150);
        }

        return null;
    }

    private static T? TryFindElement<T>(Func<T?> lookup, int timeoutSeconds)
        where T : class
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var element = lookup();
            if (element is not null)
                return element;

            Thread.Sleep(150);
        }

        return null;
    }

    private static T WaitUntilElement<T>(Func<T?> lookup, int timeoutSeconds, string description)
        where T : class
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var element = lookup();
            if (element is not null)
                return element;

            Thread.Sleep(200);
        }

        throw new TimeoutException($"Timed out waiting for {description}.");
    }

    private static void WaitUntil(Func<bool> condition, int timeoutSeconds, string description)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;

            Thread.Sleep(200);
        }

        throw new TimeoutException($"Timed out waiting for {description}.");
    }
}
