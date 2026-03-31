using System.Windows;
using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;
using HelpDesk.Infrastructure.Services;
using HelpDesk.Presentation.Helpers;
using HelpDesk.Presentation.ViewModels;
using Xunit;

namespace HelpDesk.Tests;

public sealed class SettingsPolicyAndAutomationTests
{
    [Fact]
    public void Locked_policy_state_disables_controls()
    {
        var converter = new LockedToIsEnabledConverter();

        var result = converter.Convert(PolicyState.Locked, typeof(bool), null!, System.Globalization.CultureInfo.InvariantCulture);

        Assert.IsType<bool>(result);
        Assert.False((bool)result);
    }

    [Fact]
    public void None_policy_state_hides_policy_chip()
    {
        var converter = new PolicyStateToVisibilityConverter();

        var result = converter.Convert(PolicyState.None, typeof(Visibility), null!, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void Policy_service_returns_locked_for_forced_setting()
    {
        var deployment = new DeploymentConfiguration
        {
            ForceShowNotifications = true
        };

        var state = ProductizationPolicies.GetPolicyState(deployment, new AppSettings(), "ShowNotifications");

        Assert.Equal(PolicyState.Locked, state);
    }

    [Fact]
    public void Attention_count_is_zero_when_no_attention_receipts_exist()
    {
        var receipts = new[]
        {
            new AutomationRunReceipt
            {
                Id = "ok-1",
                RuleTitle = "Quick Health",
                StartedAt = DateTime.Now.AddMinutes(-10),
                Summary = "Completed successfully",
                Outcome = AutomationRunOutcome.Completed
            }
        };

        var items = MainViewModel.BuildAutomationAttentionItems(receipts, Array.Empty<string>());

        Assert.Empty(items);
    }
}
