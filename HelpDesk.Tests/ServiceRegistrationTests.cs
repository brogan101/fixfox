using HelpDesk.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HelpDesk.Tests;

public sealed class ServiceRegistrationTests
{
    [Fact]
    public void ProgramBuildServices_ResolvesSharedUtilityServices()
    {
        using var provider = Program.BuildServices(headless: true) as ServiceProvider;

        Assert.NotNull(provider);
        Assert.NotNull(provider!.GetService<DuplicateFileService>());
        Assert.NotNull(provider.GetService<InstalledProgramsService>());
        Assert.NotNull(provider.GetService<SchedulerService>());
    }
}
