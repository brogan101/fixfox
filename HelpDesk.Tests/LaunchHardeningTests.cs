using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;
using HelpDesk.Infrastructure.Services;
using Newtonsoft.Json;
using Xunit;

namespace HelpDesk.Tests;

public sealed class LaunchHardeningTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "FixFox.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void SettingsService_RecoversFromBackup_AndFlagsUncleanPreviousSession()
    {
        Directory.CreateDirectory(_tempRoot);
        File.WriteAllText(Path.Combine(_tempRoot, "settings.json"), "{ definitely not valid json");

        var backup = new AppSettings
        {
            BehaviorProfile = "Work Laptop",
            SchemaVersion = 1,
            LastSessionEndedCleanly = false
        };
        File.WriteAllText(
            Path.Combine(_tempRoot, "settings.bak.json"),
            JsonConvert.SerializeObject(backup, Formatting.Indented));

        var service = new SettingsService(_tempRoot);
        var loaded = service.Load();

        Assert.Equal("Work Laptop", loaded.BehaviorProfile);
        Assert.Equal(ProductizationPolicies.CurrentSettingsSchemaVersion, loaded.SchemaVersion);
        Assert.True(service.LastLoadStatus.LoadedFromBackup);
        Assert.True(service.LastLoadStatus.PreviousSessionEndedUncleanly);
        Assert.Contains(service.LastLoadStatus.Notes, note => note.Contains("backup", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(Directory.GetFiles(_tempRoot), path => Path.GetFileName(path).StartsWith("settings.json.corrupt-", StringComparison.OrdinalIgnoreCase));
        Assert.True(File.Exists(Path.Combine(_tempRoot, "settings.json")));
    }

    [Fact]
    public void SettingsService_RestoresDefaults_WhenPrimaryAndBackupAreUnreadable()
    {
        Directory.CreateDirectory(_tempRoot);
        File.WriteAllText(Path.Combine(_tempRoot, "settings.json"), "{ bad");
        File.WriteAllText(Path.Combine(_tempRoot, "settings.bak.json"), "{ also bad");

        var service = new SettingsService(_tempRoot);
        var loaded = service.Load();

        Assert.True(service.LastLoadStatus.RecoveredDefaults);
        Assert.Equal("Standard", loaded.BehaviorProfile);
        Assert.True(File.Exists(Path.Combine(_tempRoot, "settings.json")));
    }

    [Fact]
    public async Task SettingsService_SerializesConcurrentSaves_WithoutCorruptingSettings()
    {
        Directory.CreateDirectory(_tempRoot);
        var service = new SettingsService(_tempRoot);
        var settings = service.Load();

        var tasks = Enumerable.Range(0, 12)
            .Select(async index =>
            {
                await Task.Yield();
                settings.WindowWidth = 1100 + index;
                settings.WindowHeight = 700 + index;
                service.Save(settings);
            });

        await Task.WhenAll(tasks);

        var reloaded = service.Load();
        Assert.True(reloaded.WindowWidth >= 1100);
        Assert.True(reloaded.WindowHeight >= 700);
        Assert.True(File.Exists(Path.Combine(_tempRoot, "settings.json")));
        Assert.False(File.Exists(Path.Combine(_tempRoot, "settings.json.tmp")));
    }

    [Fact]
    public void SettingsService_CanSaveTwice_OnFreshProfile()
    {
        Directory.CreateDirectory(_tempRoot);
        var service = new SettingsService(_tempRoot);
        var settings = service.Load();

        settings.LastSessionEndedCleanly = false;
        service.Save(settings);
        settings.ShowNotifications = false;
        service.Save(settings);

        var reloaded = service.Load();
        Assert.False(reloaded.ShowNotifications);
    }

    [Fact]
    public void SettingsService_Migrates_Legacy_Schema_Without_Throwing()
    {
        Directory.CreateDirectory(_tempRoot);
        File.WriteAllText(
            Path.Combine(_tempRoot, "settings.json"),
            JsonConvert.SerializeObject(new
            {
                SchemaVersion = 0,
                BehaviorProfile = "Standard",
                Theme = "Dark"
            }, Formatting.Indented));

        var service = new SettingsService(_tempRoot);
        var loaded = service.Load();

        Assert.Equal(ProductizationPolicies.CurrentSettingsSchemaVersion, loaded.SchemaVersion);
        Assert.False(string.IsNullOrWhiteSpace(loaded.NotificationMode));
        Assert.False(string.IsNullOrWhiteSpace(loaded.SupportBundleExportLevel));
        Assert.False(string.IsNullOrWhiteSpace(loaded.DefaultLandingPage));
    }

    [Fact]
    public void SettingsService_Writes_Valid_Json_That_RoundTrips()
    {
        Directory.CreateDirectory(_tempRoot);
        var service = new SettingsService(_tempRoot);
        var settings = service.Load();
        settings.BehaviorProfile = "Work Laptop";
        settings.ShowNotifications = false;

        service.Save(settings);

        var json = File.ReadAllText(Path.Combine(_tempRoot, "settings.json"));
        var reparsed = JsonConvert.DeserializeObject<AppSettings>(json);

        Assert.NotNull(reparsed);
        Assert.Equal("Work Laptop", reparsed!.BehaviorProfile);
        Assert.False(reparsed.ShowNotifications);
    }

    [Fact]
    public void SettingsService_Handles_Garbage_File_Gracefully()
    {
        Directory.CreateDirectory(_tempRoot);
        File.WriteAllText(Path.Combine(_tempRoot, "settings.json"), "GARBAGE");

        var service = new SettingsService(_tempRoot);
        var loaded = service.Load();

        Assert.NotNull(loaded);
        Assert.True(service.LastLoadStatus.RecoveredDefaults || service.LastLoadStatus.LoadedFromBackup);
        Assert.True(File.Exists(Path.Combine(_tempRoot, "settings.json")));
    }

    [Fact]
    public void DefaultSettings_Are_Calm_Until_Onboarding_Completes()
    {
        var defaults = ProductizationPolicies.CreateDefaultSettings();

        Assert.False(defaults.RunQuickScanOnLaunch);
        Assert.False(defaults.CheckForUpdatesOnLaunch);
        Assert.False(defaults.ShowTrayBalloons);
        Assert.True(defaults.RunFirstHealthCheckAfterSetup);
        Assert.False(defaults.OnboardingDismissed);
    }

    [Fact]
    public void BehaviorProfilePolicy_AppliesQuietPresetSafely()
    {
        var settings = new AppSettings();

        ProductizationPolicies.ApplyBehaviorProfile(settings, "Quiet");

        Assert.False(settings.ShowNotifications);
        Assert.Equal("Quiet", settings.NotificationMode);
        Assert.False(settings.MinimizeToTray);
        Assert.True(settings.PreferSafeMaintenanceDefaults);
        Assert.False(settings.RecoverInterruptedOperations);
        Assert.Equal("Basic", settings.SupportBundleExportLevel);
        Assert.False(settings.AdvancedMode);
    }

    [Fact]
    public void InterruptedRecoveryPolicy_KeepsStaleStateForInspection()
    {
        var decision = ProductizationPolicies.EvaluateInterruptedState(
            new InterruptedOperationState
            {
                DisplayTitle = "Guided repair",
                Summary = "Old recovery marker",
                StartedAt = DateTime.Now.AddDays(-4),
                CanResume = true,
                Outcome = ExecutionOutcome.Interrupted
            },
            new AppSettings { RecoverInterruptedOperations = true },
            DateTime.Now);

        Assert.True(decision.KeepForInspection);
        Assert.False(decision.ShouldResume);
        Assert.Contains("stale", decision.Notice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AppUpdateService_UsesManifestFeedAndResolvesFriendlyStatus()
    {
        Directory.CreateDirectory(_tempRoot);
        var releaseNotes = Path.Combine(_tempRoot, "notes.md");
        File.WriteAllText(releaseNotes, "# Notes");

        var manifestPath = Path.Combine(_tempRoot, "release-feed.json");
        File.WriteAllText(
            manifestPath,
            JsonConvert.SerializeObject(new
            {
                ChannelName = "Stable",
                LatestVersion = "1.1.1",
                DownloadUrl = "https://github.com/brogan101/fixfox/releases",
                ReleaseNotesPath = releaseNotes,
                Summary = "A newer FixFox build is available."
            }, Formatting.Indented));

        var settings = new FakeSettingsService
        {
            Settings = new AppSettings
            {
                CheckForUpdatesOnLaunch = true,
                UpdateFeedUrl = manifestPath
            }
        };

        var service = new AppUpdateService(settings, new FakeDeploymentConfigurationService());
        var info = await service.CheckForUpdatesAsync();

        Assert.True(info.UpdateAvailable);
        Assert.Equal("1.1.1", info.LatestVersion);
        Assert.Equal("Release feed", info.SourceName);
        Assert.Equal(releaseNotes, info.ReleaseNotesPath);
    }

    [Fact]
    public async Task AppUpdateService_FailsGracefully_WhenManifestFeedIsMissing()
    {
        var settings = new FakeSettingsService
        {
            Settings = new AppSettings
            {
                CheckForUpdatesOnLaunch = true,
                UpdateFeedUrl = Path.Combine(_tempRoot, "missing-feed.json")
            }
        };

        var service = new AppUpdateService(settings, new FakeDeploymentConfigurationService());
        var info = await service.CheckForUpdatesAsync();

        Assert.False(info.UpdateAvailable);
        Assert.Equal("Release feed", info.SourceName);
        Assert.Contains("could not read", info.Summary, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
        }
    }
}
