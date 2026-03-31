using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using HelpDesk.Domain.Models;
using Microsoft.Win32;
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

        session.WaitForControl("NavDashboard");
        session.ClickNav("NavDashboard", "DashboardPageMarker");

        var settings = JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(session.SettingsPath));
        Assert.NotNull(settings);
        Assert.False(settings!.OnboardingDismissed);
    }

    [StaFact]
    public void Shell_DeviceHealth_Toolbox_And_Automation_Flows_Work_Through_The_Real_App()
    {
        using var session = UiTestSession.Launch(seedCompletedSetup: true);

        session.WaitForControl("NavDashboard");
        session.ClickNav("NavDashboard", "DashboardPageMarker");
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
        session.WaitForControl("Automation_AttentionTab");
        WaitUntil(() =>
        {
            var resultText = session.TryFindElement("Automation_LastResultText");
            return resultText is not null
                && !resultText.Name.Contains("No automation has run yet.", StringComparison.OrdinalIgnoreCase);
        }, 90, "automation last-result text to update after quick health");
    }

    [StaFact]
    public void Fixes_Diagnosis_And_History_Empty_State_Work()
    {
        using var session = UiTestSession.Launch(seedCompletedSetup: true);

        session.WaitForControl("NavDashboard");
        session.ClickNav("NavHistory", "HistoryPageMarker");
        var emptyStateRepairLibraryButton = WaitUntilElement(
            () => session.MainWindow.FindAllDescendants(cf => cf.ByText("Open Repair Library"))
                .FirstOrDefault()?.AsButton(),
            10,
            "history empty-state repair library button");
        UiTestSession.Click(emptyStateRepairLibraryButton);
        session.WaitForPage("FixCenterPageMarker");
        session.WaitForControl("FixCenter_CategoryRail");

        var fixSearchBox = session.FindTextBox("FixCenter_Search");
        fixSearchBox.Text = "dns";

        var firstFix = WaitUntilElement(
            () => session.FindElement("FixCenter_FixList").FindFirstDescendant(cf => cf.ByControlType(ControlType.ListItem)),
            15,
            "first fix list item");
        UiTestSession.Click(firstFix);
        WaitUntil(() =>
        {
            return session.MainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                .Any(element => string.Equals(element.Name, "Estimated duration", StringComparison.OrdinalIgnoreCase));
        }, 15, "expanded repair panel for flush-dns");

        var allFixes = WaitUntilElement(
            () => session.MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("FixCenter_Category_AllFixes")),
            15,
            "all fixes rail item");
        UiTestSession.Click(allFixes);
        WaitUntil(() =>
        {
            var header = session.MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("FixCenter_ListHeader"));
            return header is not null
                && header.Name.Contains("All Fixes", StringComparison.OrdinalIgnoreCase)
                && header.Name.Any(char.IsDigit);
        }, 15, "Fix Center list header to show the all-fixes count");

        session.ClickNav("NavSymptomChecker", "SymptomCheckerPageMarker");
        var symptomInput = session.FindTextBox("SymptomInputBox");
        symptomInput.Text = "wifi not working and browser timeout";
        session.ClickButton("SymptomAnalyzeButton");
        session.WaitForControl("Diagnosis_TopResult");
        Assert.True(
            session.MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("Diagnosis_RunnerUp_1")) is not null
            || session.MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("Diagnosis_NextStep_RunFix")) is not null);
    }

    [StaFact]
    public void FixCenter_Context_Menu_And_Device_Health_Sections_Work()
    {
        using var session = UiTestSession.Launch(seedCompletedSetup: true);

        session.WaitForControl("NavDashboard");
        session.ClickNav("NavFixes", "FixCenterPageMarker");
        session.WaitForControl("FixCenter_CategoryRail");
        OpenContextMenuInFixList(session, "ContextMenu_Fix");
        Assert.NotNull(session.TryFindElement("Card_Fix_Run"));

        session.ClickNav("NavSystemInfo", "SystemInfoPageMarker");
        session.PageDown();
        var storageToggle = session.FindToggle("DeviceHealth_Section_Storage_Toggle");
        storageToggle.Toggle();
        ScrollUntilControlAppears(session, "DeviceHealth_Section_Storage_Marker", 15);
        var storageMarker = session.FindElement("DeviceHealth_Section_Storage_Marker");
        WaitUntil(() =>
        {
            return storageToggle.ToggleState == ToggleState.On
                && storageMarker.BoundingRectangle.Height > 8;
        }, 10, "storage section to expand");

        storageToggle.Toggle();
        WaitUntil(() =>
        {
            return storageToggle.ToggleState == ToggleState.Off;
        }, 10, "storage section to collapse");
    }

    [StaFact]
    public void App_Center_Browser_And_Startup_Support_Surfaces_Are_Reachable()
    {
        using var session = UiTestSession.Launch(seedCompletedSetup: true);

        session.WaitForControl("NavDashboard");
        session.ClickNav("NavSystemInfo", "SystemInfoPageMarker");

        var startupToggle = session.FindToggle("DeviceHealth_Section_StartupPerformance_Toggle");
        if (startupToggle.ToggleState == ToggleState.Off)
            startupToggle.Toggle();

        ScrollUntilControlAppears(session, "AppCenter_List", 30);
        var appList = session.FindElement("AppCenter_List");
        var firstApp = WaitUntilElement(
            () => appList.FindFirstDescendant(cf => cf.ByControlType(ControlType.ListItem)),
            20,
            "first installed program list item");
        UiTestSession.Click(firstApp);
        session.WaitForControl("AppCenter_Detail_OpenLocation");
        Assert.NotNull(session.TryFindElement("AppCenter_DetailPane"));
        session.ClickButton("AppCenter_Detail_Close");

        ScrollUntilControlAppears(session, "Startup_Item_1_ImpactChip", 15);

        session.PageDown();
        var networkToggle = session.FindToggle("DeviceHealth_Section_Network_Toggle");
        if (networkToggle.ToggleState == ToggleState.Off)
            networkToggle.Toggle();

        if (AnySupportedBrowserInstalled())
        {
            WaitUntil(() =>
            {
                session.PageDown();
                return session.MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("Browser_Chrome_Section")) is not null
                    || session.MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("Browser_Edge_Section")) is not null
                    || session.MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("Browser_Firefox_Section")) is not null
                    || session.MainWindow.FindAllDescendants(cf => cf.ByText("No supported browsers were detected on this device.")).Any();
            }, 30, "browser extension review section");
        }
    }

    [StaFact]
    public void Simplified_Mode_Shows_Three_Nav_Items_And_Fix_My_Pc_Path()
    {
        using var session = UiTestSession.Launch(seedCompletedSetup: true);

        session.WaitForControl("NavDashboard");
        session.ClickNav("NavSettings", "SettingsPageMarker");

        var simplifiedToggle = session.FindToggle("Settings_SimplifiedMode_Toggle");
        if (simplifiedToggle.ToggleState == ToggleState.Off)
            simplifiedToggle.Toggle();

        WaitUntil(() => CountVisibleNavItems(session) == 3, 15, "simplified navigation to collapse to three items");

        session.ClickNav("NavFixes", "FixMyPCPageMarker");
        session.WaitForControl("FixMyPC_Problem_Slow");
        session.ClickButton("FixMyPC_Problem_Slow");
        session.WaitForControl("SimplifiedConfirmationDialog");
        session.ClickButton("SimplifiedConfirmation_Cancel");

        session.ClickNav("NavSettings", "SettingsPageMarker");
        simplifiedToggle = session.FindToggle("Settings_SimplifiedMode_Toggle");
        if (simplifiedToggle.ToggleState == ToggleState.On)
            simplifiedToggle.Toggle();

        WaitUntil(() => CountVisibleNavItems(session) >= 8, 15, "full navigation to return");
    }

    private static void OpenContextMenu(UiTestSession session, string automationId, string menuAutomationId)
    {
        var anchor = WaitUntilElement(
            () => session.MainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
            15,
            $"{automationId} anchor");

        var candidate = anchor;
        for (var depth = 0; depth < 6 && candidate is not null; depth++)
        {
            try
            {
                UiTestSession.RightClick(candidate);
                var menu = TryFindElement(
                    () => session.Automation.GetDesktop().FindFirstDescendant(cf => cf.ByAutomationId(menuAutomationId)),
                    2);
                if (menu is not null)
                    return;
            }
            catch
            {
            }

            candidate = candidate.Parent;
        }

        throw new TimeoutException($"Timed out waiting for context menu {menuAutomationId}.");
    }

    private static int CountVisibleNavItems(UiTestSession session)
        => session.MainWindow.FindAllDescendants()
            .Count(element =>
            {
                try
                {
                    var automationId = element.AutomationId;
                    return !string.IsNullOrWhiteSpace(automationId)
                        && automationId.StartsWith("Nav", StringComparison.OrdinalIgnoreCase)
                        && element.ControlType == ControlType.Button
                        && element.IsOffscreen == false;
                }
                catch
                {
                    return false;
                }
            });

    private static void OpenContextMenuByText(UiTestSession session, string text, string menuAutomationId)
    {
        var anchor = WaitUntilElement(
            () => session.MainWindow.FindAllDescendants(cf => cf.ByText(text)).FirstOrDefault(),
            15,
            $"{text} anchor");

        var candidate = anchor;
        for (var depth = 0; depth < 6 && candidate is not null; depth++)
        {
            try
            {
                UiTestSession.RightClick(candidate);
                var menu = TryFindElement(
                    () => session.Automation.GetDesktop().FindFirstDescendant(cf => cf.ByAutomationId(menuAutomationId)),
                    2);
                if (menu is not null)
                    return;
            }
            catch
            {
            }

            candidate = candidate.Parent;
        }

        throw new TimeoutException($"Timed out waiting for context menu {menuAutomationId}.");
    }

    private static void OpenContextMenuInFixList(UiTestSession session, string menuAutomationId)
    {
        var anchor = WaitUntilElement(
            () => session.FindElement("FixCenter_FixList")
                .FindFirstDescendant(cf => cf.ByAutomationId("Card_Fix_Run")),
            15,
            "first fix run button");

        try
        {
            anchor.Focus();
            Keyboard.TypeSimultaneously(VirtualKeyShort.SHIFT, VirtualKeyShort.F10);
            var keyboardMenu = TryFindElement(
                () => session.Automation.GetDesktop().FindFirstDescendant(cf => cf.ByAutomationId(menuAutomationId))
                    ?? session.Automation.GetDesktop().FindFirstDescendant(cf => cf.ByControlType(ControlType.Menu)),
                2);
            if (keyboardMenu is not null)
                return;
        }
        catch
        {
        }

        var candidate = anchor;
        for (var depth = 0; depth < 6 && candidate is not null; depth++)
        {
            try
            {
                UiTestSession.RightClick(candidate);
                var menu = TryFindElement(
                    () => session.Automation.GetDesktop().FindFirstDescendant(cf => cf.ByAutomationId(menuAutomationId))
                        ?? session.Automation.GetDesktop().FindFirstDescendant(cf => cf.ByControlType(ControlType.Menu)),
                    2);
                if (menu is not null)
                    return;
            }
            catch
            {
            }

            candidate = candidate.Parent;
        }
    }

    private static void ClickVisibleText(UiTestSession session, string text)
    {
        var element = WaitUntilElement(
            () => session.MainWindow.FindAllDescendants(cf => cf.ByText(text)).FirstOrDefault(),
            15,
            $"{text} text");
        UiTestSession.Click(element);
    }

    [StaFact]
    public void Settings_And_Command_Palette_Work()
    {
        using var session = UiTestSession.Launch(seedCompletedSetup: true);

        session.WaitForControl("NavDashboard");
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
        var commandPalette = session.FindTextBox("GlobalSearch_Input");
        commandPalette.Text = "Home";
        var firstResult = WaitUntilElement(
            () => session.MainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                .FirstOrDefault(element =>
                    element.AutomationId.StartsWith("GlobalSearch_Result_", StringComparison.OrdinalIgnoreCase)),
            15,
            "global search result button");
        UiTestSession.Click(firstResult);
        session.WaitForControl("DashboardPageMarker");
    }

    [StaFact]
    public void Support_Package_Flow_Creates_Local_Evidence()
    {
        using var session = UiTestSession.Launch(seedCompletedSetup: true);

        session.WaitForControl("NavDashboard");
        session.ClickNav("NavHandoff", "HandoffPageMarker");
        session.FindButton("HandoffCreateSupportPackageButton").Invoke();
        var evidenceRoot = Path.Combine(session.ProfileRoot, "evidence-bundles");
        WaitUntil(() => Directory.Exists(evidenceRoot) && Directory.GetDirectories(evidenceRoot).Any(), 150,
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
            session.WaitForControl("NavDashboard");
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
            secondSession.WaitForControl("NavDashboard");
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

    private static void ScrollUntilControlAppears(UiTestSession session, string automationId, int timeoutSeconds)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var element = session.TryFindElement(automationId);
            if (element is not null && !element.Properties.IsOffscreen.ValueOrDefault && element.BoundingRectangle.Height > 4)
                return;

            session.PageDown();
            Thread.Sleep(250);
        }

        throw new TimeoutException($"Timed out waiting for control {automationId}.");
    }

    private static bool AnySupportedBrowserInstalled()
    {
        return HasAppPath(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe")
            || HasAppPath(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe")
            || HasAppPath(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\firefox.exe");
    }

    private static bool HasAppPath(string registryPath)
    {
        using var key = Registry.LocalMachine.OpenSubKey(registryPath);
        return !string.IsNullOrWhiteSpace(key?.GetValue(string.Empty)?.ToString());
    }
}
