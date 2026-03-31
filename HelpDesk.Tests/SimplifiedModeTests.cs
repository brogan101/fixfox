using HelpDesk.Application.Interfaces;
using HelpDesk.Domain.Enums;
using HelpDesk.Infrastructure.Services;
using HelpDesk.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HelpDesk.Tests;

public sealed class SimplifiedModeTests
{
    [Fact]
    public void TextSubstitutionService_Returns_Simple_Risk_Label_When_Simplified_Mode_Is_On()
    {
        var service = new TextSubstitutionService();

        service.SetSimplifiedMode(true);

        Assert.Equal("Safe to run", service.Get("RiskLevel_Safe"));
    }

    [Fact]
    public void TextSubstitutionService_Returns_Technical_Risk_Label_When_Simplified_Mode_Is_Off()
    {
        var service = new TextSubstitutionService();

        service.SetSimplifiedMode(false);

        Assert.Equal("Safe", service.Get("RiskLevel_Safe"));
    }

    [Fact]
    public void Simplified_Mode_Navigation_Drops_To_Three_Items()
    {
        var pages = MainViewModel.GetNavigationPages(simplifiedModeEnabled: true);

        Assert.Equal(3, pages.Count);
        Assert.Equal([Page.Dashboard, Page.FixMyPc, Page.Settings], pages);
    }

    [Fact]
    public void Fix_My_Pc_Sound_Button_Maps_To_A_Registered_Target()
    {
        var option = MainViewModel.BuildSimplifiedProblemOptions()
            .Single(item => item.Key == "sound");

        var services = Program.BuildServices(headless: true);
        var catalog = services.GetRequiredService<IFixCatalogService>();
        var runbooks = services.GetRequiredService<IRunbookCatalogService>();

        var hasTarget = catalog.GetById(option.TargetId) is not null
            || runbooks.Runbooks.Any(runbook => string.Equals(runbook.Id, option.TargetId, StringComparison.OrdinalIgnoreCase));

        Assert.True(hasTarget);
    }

    [Fact]
    public void All_Simplified_Problem_Buttons_Map_To_Valid_Targets()
    {
        var options = MainViewModel.BuildSimplifiedProblemOptions();

        var services = Program.BuildServices(headless: true);
        var catalog = services.GetRequiredService<IFixCatalogService>();
        var runbooks = services.GetRequiredService<IRunbookCatalogService>();

        foreach (var option in options)
        {
            var isValid = option.ActionKind switch
            {
                SupportActionKind.Fix => catalog.GetById(option.TargetId) is not null,
                SupportActionKind.Runbook => runbooks.Runbooks.Any(runbook => string.Equals(runbook.Id, option.TargetId, StringComparison.OrdinalIgnoreCase)),
                SupportActionKind.Page => Enum.TryParse<Page>(option.TargetId, ignoreCase: true, out _),
                SupportActionKind.GlobalSearch => string.Equals(option.TargetId, "global-search", StringComparison.OrdinalIgnoreCase),
                _ => false
            };

            Assert.True(isValid, $"Simplified problem '{option.Key}' did not resolve to a valid target.");
        }
    }
}
