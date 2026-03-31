using HelpDesk.Domain.Enums;
using HelpDesk.Infrastructure.Services;
using Xunit;

namespace HelpDesk.Tests;

public sealed class SupportCenterDeepDiveTests
{
    [Fact]
    public void Browser_Extension_With_AllUrls_Is_High_Risk()
    {
        var service = new BrowserExtensionReviewService();

        var risk = service.ClassifyRisk(["<all_urls>"]);

        Assert.Equal(BrowserPermissionRisk.High, risk);
    }

    [Fact]
    public void Browser_Extension_With_Storage_Only_Is_Not_High_Risk()
    {
        var service = new BrowserExtensionReviewService();

        var risk = service.ClassifyRisk(["storage"]);

        Assert.True(risk is BrowserPermissionRisk.Low or BrowserPermissionRisk.Medium);
    }

    [Fact]
    public void Startup_Item_With_Delay_Greater_Than_Two_Seconds_Is_High_Impact()
    {
        Assert.Equal(StartupImpactLevel.High, StartupAppsService.ClassifyImpact(2501));
    }

    [Fact]
    public void Startup_Item_With_Delay_Under_Half_A_Second_Is_Low_Impact()
    {
        Assert.Equal(StartupImpactLevel.Low, StartupAppsService.ClassifyImpact(320));
    }

    [Fact]
    public void Msi_App_With_Product_Code_Is_Repairable()
    {
        var app = new InstalledProgram(
            "Contoso App",
            "1.0",
            "Contoso",
            null,
            0,
            "msiexec /x {11111111-2222-3333-4444-555555555555}",
            "",
            "",
            "",
            "{11111111-2222-3333-4444-555555555555}");

        Assert.True(app.IsRepairAvailable);
        Assert.True(app.IsMsiApp);
    }
}
