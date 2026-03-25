using HelpDesk.Application.Interfaces;
using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;
using HelpDesk.Infrastructure.Services;
using Xunit;

namespace HelpDesk.Tests;

public sealed class CatalogAndEditionTests
{
    [Fact]
    public void MergedFixCatalogService_MergesProvidersAndAvoidsDuplicateFixIds()
    {
        var providers = new IFixCatalogProvider[]
        {
            new StubProvider(
                categories:
                [
                    new FixCategory
                    {
                        Id = "network",
                        Title = "Network",
                        Fixes =
                        [
                            new FixItem { Id = "flush-dns", Title = "Flush DNS", Description = "Fix DNS" }
                        ]
                    }
                ]),
            new StubProvider(
                categories:
                [
                    new FixCategory
                    {
                        Id = "network",
                        Title = "Network",
                        Fixes =
                        [
                            new FixItem { Id = "flush-dns", Title = "Flush DNS Duplicate", Description = "Duplicate" },
                            new FixItem { Id = "renew-ip", Title = "Renew IP", Description = "Renew IP" }
                        ]
                    }
                ])
        };

        var merged = new MergedFixCatalogService(providers);

        Assert.Single(merged.Categories);
        Assert.Equal(2, merged.Categories[0].Fixes.Count);
        Assert.NotNull(merged.GetById("renew-ip"));
    }

    [Fact]
    public void ExternalCatalogProvider_LoadsRepairPackFromCatalogFolder()
    {
        var packDir = Path.Combine(AppContext.BaseDirectory, "Catalog", "RepairPacks");
        Directory.CreateDirectory(packDir);
        var packPath = Path.Combine(packDir, "test-pack.json");
        File.WriteAllText(packPath,
            """
            {
              "categories": [
                {
                  "id": "browser",
                  "title": "Browser & Web Apps",
                  "description": "Browser fixes"
                }
              ],
              "repairs": [
                {
                  "id": "clear-web-cache-pack",
                  "title": "Clear web cache",
                  "description": "Clears web cache",
                  "categoryId": "browser",
                  "script": "Write-Output 'ok'",
                  "keywords": [ "browser slow" ]
                }
              ]
            }
            """);

        try
        {
            var settings = new FakeSettingsService { Settings = new AppSettings { EnableExtensionCatalogs = true } };
            var provider = new ExternalCatalogProvider(settings, new FakeAppLogger());

            Assert.Contains(provider.Repairs, repair => repair.Id == "clear-web-cache-pack");
            Assert.Contains(provider.Categories, category => category.Id == "browser");
        }
        finally
        {
            File.Delete(packPath);
        }
    }

    [Fact]
    public void EditionCapabilityService_KeepsCoreSupportCapabilitiesAvailableInBasicEdition()
    {
        var settings = new FakeSettingsService
        {
            Settings = new AppSettings { Edition = AppEdition.Basic }
        };

        var service = new EditionCapabilityService(settings, new FakeDeploymentConfigurationService());
        var snapshot = service.GetSnapshot();

        Assert.Equal(AppEdition.Basic, snapshot.Edition);
        Assert.Equal(CapabilityState.Available, snapshot.EvidenceBundles);
        Assert.Equal(CapabilityState.Available, snapshot.Runbooks);
        Assert.Equal(CapabilityState.Available, snapshot.DeepRepairs);
        Assert.Equal(CapabilityState.UpgradeRequired, snapshot.AdvancedMode);
        Assert.Equal(CapabilityState.UpgradeRequired, snapshot.TechnicianExports);
        Assert.Equal(CapabilityState.UpgradeRequired, snapshot.WhiteLabelBranding);
    }

    [Fact]
    public void CatalogProjection_KeepsCoreAutomationAndRescueRepairsAvailableInBasicEdition()
    {
        var category = new FixCategory
        {
            Id = "performance",
            Title = "Performance & Cleanup",
            Fixes =
            [
                new FixItem
                {
                    Id = "clear-temp-files",
                    Title = "Clear all temp files",
                    Description = "Remove low-risk temp file clutter.",
                    Type = FixType.Silent,
                    RequiresAdmin = true,
                    Script = "Write-Output 'ok'"
                }
            ]
        };

        var repair = CatalogProjection.ToRepairDefinition(category, category.Fixes[0]);

        Assert.Equal(AppEdition.Basic, repair.MinimumEdition);
        Assert.Equal(RepairTier.SafeUser, repair.Tier);
        Assert.True(repair.RequiresAdmin);
    }

    [Fact]
    public void EditionCapabilityService_RespectsManagedPolicyOverrides()
    {
        var settings = new FakeSettingsService
        {
            Settings = new AppSettings { Edition = AppEdition.Pro, AdvancedMode = true }
        };
        var deployment = new FakeDeploymentConfigurationService
        {
            Current = new DeploymentConfiguration
            {
                ManagedMode = true,
                AllowAdvancedMode = false,
                RestrictTechnicianExports = true,
                DisableDeepRepairs = true
            }
        };

        var service = new EditionCapabilityService(settings, deployment);
        var snapshot = service.GetSnapshot();

        Assert.True(snapshot.ManagedMode);
        Assert.Equal(CapabilityState.ManagedOff, snapshot.AdvancedMode);
        Assert.Equal(CapabilityState.ManagedOff, snapshot.TechnicianExports);
        Assert.Equal(CapabilityState.ManagedOff, snapshot.DeepRepairs);
    }

    [Fact]
    public void DeploymentConfigurationService_AppliesForcedPolicyDefaults()
    {
        var settings = new AppSettings
        {
            OnboardingDismissed = false,
            BehaviorProfile = "Standard",
            SupportBundleExportLevel = "Technician",
            AdvancedMode = true
        };

        var service = new FakeDeploymentConfigurationService
        {
            Current = new DeploymentConfiguration
            {
                DefaultBehaviorProfile = "Work Laptop",
                ForceNotificationMode = "Important Only",
                RestrictTechnicianExports = true,
                AllowAdvancedMode = false
            }
        };

        service.ApplyPolicy(settings);

        Assert.Equal("Work Laptop", settings.BehaviorProfile);
        Assert.Equal("Important Only", settings.NotificationMode);
        Assert.Equal("Basic", settings.SupportBundleExportLevel);
        Assert.False(settings.AdvancedMode);
    }

    [Fact]
    public void KnowledgeBaseService_ShipsRealLocalHelpLinksByDefault()
    {
        var settings = new FakeSettingsService
        {
            Settings = new AppSettings()
        };

        var service = new KnowledgeBaseService(settings, new FakeDeploymentConfigurationService());

        Assert.NotEmpty(service.Entries);
        Assert.Contains(service.Entries, entry => entry.Key == "quick-start");
        Assert.Contains(service.Entries, entry => entry.Key == "troubleshooting-and-faq");
        Assert.DoesNotContain(service.Entries, entry => entry.Url.Contains("example.com", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BrandingConfigurationService_UsesWhiteLabelIdentityForMspEdition()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "FixFox.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var brandingPath = Path.Combine(tempRoot, "branding.json");
        File.WriteAllText(
            brandingPath,
            """
            {
              "AppName": "Northwind Support",
              "AppSubtitle": "Managed workplace support",
              "VendorName": "Northwind",
              "SupportDisplayName": "Northwind IT",
              "SupportEmail": "support@northwind.test",
              "SupportPortalLabel": "Open Northwind support",
              "SupportPortalUrl": "https://support.northwind.test"
            }
            """);

        try
        {
            var settings = new FakeSettingsService
            {
                Settings = new AppSettings
                {
                    Edition = AppEdition.ManagedServiceProvider,
                    BrandingConfigPath = brandingPath
                }
            };

            var service = new BrandingConfigurationService(settings, new FakeDeploymentConfigurationService());

            Assert.Equal("Northwind Support", service.Current.AppName);
            Assert.Equal("Managed workplace support", service.Current.AppSubtitle);
            Assert.Equal("Northwind IT", service.Current.SupportDisplayName);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void BrandingConfigurationService_KeepsConsumerIdentityInBasicEdition()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "FixFox.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var brandingPath = Path.Combine(tempRoot, "branding.json");
        File.WriteAllText(
            brandingPath,
            """
            {
              "AppName": "Custom Brand",
              "AppSubtitle": "Custom subtitle"
            }
            """);

        try
        {
            var settings = new FakeSettingsService
            {
                Settings = new AppSettings
                {
                    Edition = AppEdition.Basic,
                    BrandingConfigPath = brandingPath
                }
            };

            var service = new BrandingConfigurationService(settings, new FakeDeploymentConfigurationService());

            Assert.Equal("FixFox", service.Current.AppName);
            Assert.Equal("Windows support and repair workspace", service.Current.AppSubtitle);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private sealed class StubProvider : IFixCatalogProvider
    {
        public StubProvider(
            IReadOnlyList<FixCategory>? categories = null,
            IReadOnlyList<FixBundle>? bundles = null,
            IReadOnlyList<RunbookDefinition>? runbooks = null,
            IReadOnlyList<RepairDefinition>? repairs = null,
            IReadOnlyList<MasterCategoryDefinition>? masterCategories = null)
        {
            Categories = categories ?? [];
            Bundles = bundles ?? [];
            Runbooks = runbooks ?? [];
            Repairs = repairs ?? [];
            MasterCategories = masterCategories ?? [];
        }

        public string Name => "stub";
        public IReadOnlyList<FixCategory> Categories { get; }
        public IReadOnlyList<FixBundle> Bundles { get; }
        public IReadOnlyList<RunbookDefinition> Runbooks { get; }
        public IReadOnlyList<RepairDefinition> Repairs { get; }
        public IReadOnlyList<MasterCategoryDefinition> MasterCategories { get; }
    }
}
