using HelpDesk.Presentation.ViewModels;
using Xunit;

namespace HelpDesk.Tests;

public sealed class RecoveryDecisionTreeTests
{
    [Fact]
    public void Step1_YesWindowsStarts_LandsOnStep2()
    {
        var tree = new RecoveryDecisionTreeViewModel();

        tree.SelectAnswer("YesWindowsStarts");

        Assert.Equal(RecoveryDecisionStepKind.Step2, tree.CurrentStep);
        Assert.Equal("Recovery_Step_2", tree.CurrentStepAutomationId);
    }

    [Fact]
    public void BackNavigation_FromStep2_ReturnsToStep1()
    {
        var tree = new RecoveryDecisionTreeViewModel();
        tree.SelectAnswer("YesWindowsStarts");

        tree.GoBack();

        Assert.Equal(RecoveryDecisionStepKind.Step1, tree.CurrentStep);
        Assert.Equal("Recovery_Step_1", tree.CurrentStepAutomationId);
    }
}
