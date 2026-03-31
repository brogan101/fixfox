using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace HelpDesk.Infrastructure.Services;

public sealed class BrowserExtensionReviewService
{
    private static readonly HashSet<string> HighRiskPermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        "tabs", "history", "webRequest", "webRequestBlocking", "<all_urls>", "cookies", "browsingData", "management", "debugger"
    };

    private static readonly HashSet<string> MediumRiskPermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        "activeTab", "storage", "identity"
    };

    public IReadOnlyList<BrowserExtensionSection> GetSections()
    {
        var sections = new List<BrowserExtensionSection>();

        TryAddChromiumSection(sections, "Chrome", @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe", Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Google", "Chrome", "User Data", "Default", "Extensions"), "chrome://extensions/");

        TryAddChromiumSection(sections, "Edge", @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe", Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Edge", "User Data", "Default", "Extensions"), "edge://extensions/");

        TryAddFirefoxSection(sections);
        return sections;
    }

    public BrowserPermissionRisk ClassifyRisk(IEnumerable<string> permissions)
    {
        var values = permissions
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToList();
        if (values.Any(value => HighRiskPermissions.Contains(value)))
            return BrowserPermissionRisk.High;
        if (values.Any(value => MediumRiskPermissions.Contains(value)))
            return BrowserPermissionRisk.Medium;
        return BrowserPermissionRisk.Low;
    }

    private void TryAddChromiumSection(List<BrowserExtensionSection> sections, string browserName, string appPathKey, string extensionRoot, string disableUri)
    {
        var browserPath = GetAppPath(appPathKey);
        if (string.IsNullOrWhiteSpace(browserPath))
            return;

        var extensions = new List<BrowserExtensionEntry>();
        if (Directory.Exists(extensionRoot))
        {
            foreach (var extensionIdDir in Directory.EnumerateDirectories(extensionRoot))
            {
                var manifestPath = Directory.EnumerateDirectories(extensionIdDir)
                    .Select(versionDir => Path.Combine(versionDir, "manifest.json"))
                    .FirstOrDefault(File.Exists);
                if (manifestPath is null)
                    continue;

                try
                {
                    var manifest = JObject.Parse(File.ReadAllText(manifestPath));
                    var permissions = manifest["permissions"]?.Values<string?>()
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!)
                        .ToList() ?? [];
                    var risk = ClassifyRisk(permissions);
                    extensions.Add(new BrowserExtensionEntry
                    {
                        BrowserName = browserName,
                        Name = ResolveManifestString(manifest["name"]?.ToString(), Path.GetFileName(extensionIdDir)),
                        Description = ResolveManifestString(manifest["description"]?.ToString(), "No description published."),
                        Version = manifest["version"]?.ToString() ?? "",
                        IsEnabled = true,
                        Permissions = permissions,
                        RiskLevel = risk,
                        RiskReason = BuildRiskReason(permissions, risk),
                        DisableUri = disableUri
                    });
                }
                catch
                {
                }
            }
        }

        sections.Add(new BrowserExtensionSection
        {
            BrowserName = browserName,
            LaunchPath = browserPath,
            Extensions = extensions.OrderByDescending(entry => entry.RiskLevel).ThenBy(entry => entry.Name).ToList()
        });
    }

    private void TryAddFirefoxSection(List<BrowserExtensionSection> sections)
    {
        var browserPath = GetAppPath(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\firefox.exe");
        if (string.IsNullOrWhiteSpace(browserPath))
            return;

        var profilesRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Mozilla", "Firefox", "Profiles");
        var extensions = new List<BrowserExtensionEntry>();
        if (Directory.Exists(profilesRoot))
        {
            foreach (var profileDir in Directory.EnumerateDirectories(profilesRoot))
            {
                var extensionsJsonPath = Path.Combine(profileDir, "extensions.json");
                if (!File.Exists(extensionsJsonPath))
                    continue;

                try
                {
                    var root = JObject.Parse(File.ReadAllText(extensionsJsonPath));
                    foreach (var addon in root["addons"]?.Children<JObject>() ?? [])
                    {
                        if (addon["type"]?.ToString() != "extension")
                            continue;

                        var permissions = addon["userPermissions"]?["permissions"]?.Values<string?>()
                            .Where(value => !string.IsNullOrWhiteSpace(value))
                            .Select(value => value!)
                            .ToList() ?? [];
                        var risk = ClassifyRisk(permissions);
                        extensions.Add(new BrowserExtensionEntry
                        {
                            BrowserName = "Firefox",
                            Name = addon["defaultLocale"]?["name"]?.ToString() ?? addon["name"]?.ToString() ?? "Firefox extension",
                            Description = addon["defaultLocale"]?["description"]?.ToString() ?? "",
                            Version = addon["version"]?.ToString() ?? "",
                            IsEnabled = addon["active"]?.Value<bool?>() ?? false,
                            Permissions = permissions,
                            RiskLevel = risk,
                            RiskReason = BuildRiskReason(permissions, risk),
                            DisableUri = "about:addons"
                        });
                    }
                }
                catch
                {
                }
            }
        }

        sections.Add(new BrowserExtensionSection
        {
            BrowserName = "Firefox",
            LaunchPath = browserPath,
            Extensions = extensions.OrderByDescending(entry => entry.RiskLevel).ThenBy(entry => entry.Name).ToList()
        });
    }

    private static string GetAppPath(string registryPath)
    {
        using var key = Registry.LocalMachine.OpenSubKey(registryPath);
        return key?.GetValue(string.Empty)?.ToString() ?? "";
    }

    private static string ResolveManifestString(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) || value.StartsWith("__MSG_", StringComparison.OrdinalIgnoreCase)
            ? fallback
            : value;

    private static string BuildRiskReason(IReadOnlyList<string> permissions, BrowserPermissionRisk risk)
    {
        if (permissions.Count == 0)
            return "This extension does not declare elevated browser permissions.";

        var joined = string.Join(", ", permissions.Take(3));
        return risk switch
        {
            BrowserPermissionRisk.High => $"High-risk permissions detected: {joined}.",
            BrowserPermissionRisk.Medium => $"Moderate browser permissions detected: {joined}.",
            _ => $"Published permissions: {joined}."
        };
    }
}

public sealed class WorkFromHomeDependencyService
{
    public async Task<IReadOnlyList<WorkResourceCheckCard>> BuildChecksAsync()
    {
        var mappedDrives = await GetMappedDrivesAsync();
        var credentials = await GetCredentialTargetsAsync();
        var vpnConnected = HasActiveVpnAdapter();

        var cards = new List<WorkResourceCheckCard>();
        foreach (var drive in mappedDrives)
        {
            var hints = new List<WorkResourceDependencyHint>();
            IPAddress[] addresses;
            try
            {
                addresses = await Dns.GetHostAddressesAsync(drive.Host);
            }
            catch
            {
                addresses = [];
            }

            if (addresses.Length == 0)
            {
                hints.Add(new WorkResourceDependencyHint
                {
                    Type = WorkResourceDependencyHintType.RequiresSpecificDns,
                    Explanation = "This host did not resolve with the current DNS path, which often means a corporate DNS server or VPN DNS suffix is missing."
                });
            }

            var smbReachable = await CanReachPortAsync(drive.Host, 445, 1500);
            if (!smbReachable && !vpnConnected)
            {
                hints.Add(new WorkResourceDependencyHint
                {
                    Type = WorkResourceDependencyHintType.RequiresVpn,
                    Explanation = "The share host is not reachable on SMB and no active VPN-style adapter is connected."
                });
            }

            if (credentials.Any(target => target.Contains(drive.Host, StringComparison.OrdinalIgnoreCase)))
            {
                hints.Add(new WorkResourceDependencyHint
                {
                    Type = WorkResourceDependencyHintType.RequiresCredentialRefresh,
                    Explanation = "Credential Manager already has a saved entry for this host, so a stale password or token may be blocking access."
                });
            }

            if (await HasCertificateErrorAsync(drive.Host))
            {
                hints.Add(new WorkResourceDependencyHint
                {
                    Type = WorkResourceDependencyHintType.RequiresCertificate,
                    Explanation = "HTTPS reached the host, but the TLS handshake reported a certificate problem that may require a client certificate or trust fix."
                });
            }

            cards.Add(new WorkResourceCheckCard
            {
                Title = $"{drive.DriveLetter} -> {drive.RemotePath}",
                TargetHost = drive.Host,
                Summary = smbReachable
                    ? "SMB is reachable from this device."
                    : "SMB did not answer on port 445 from this device.",
                StatusText = smbReachable ? "Connected path" : "Needs review",
                IsHealthy = smbReachable,
                DependencyHints = hints
            });
        }

        return cards;
    }

    private static async Task<IReadOnlyList<(string DriveLetter, string RemotePath, string Host)>> GetMappedDrivesAsync()
    {
        var startInfo = new ProcessStartInfo("cmd.exe", "/c net use")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process is null)
            return [];

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        var results = new List<(string DriveLetter, string RemotePath, string Host)>();
        foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.Contains(@"\\", StringComparison.Ordinal))
                continue;

            var parts = Regex.Split(line.Trim(), @"\s+")
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .ToArray();
            var remote = parts.FirstOrDefault(part => part.StartsWith(@"\\", StringComparison.Ordinal));
            if (string.IsNullOrWhiteSpace(remote))
                continue;

            var driveLetter = parts.FirstOrDefault(part => Regex.IsMatch(part, "^[A-Z]:$", RegexOptions.IgnoreCase)) ?? "UNC";
            var host = remote.TrimStart('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            if (!string.IsNullOrWhiteSpace(host))
                results.Add((driveLetter, remote, host));
        }

        return results;
    }

    private static async Task<IReadOnlyList<string>> GetCredentialTargetsAsync()
    {
        var startInfo = new ProcessStartInfo("cmd.exe", "/c cmdkey /list")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process is null)
            return [];

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("Target:", StringComparison.OrdinalIgnoreCase))
            .Select(line => line["Target:".Length..].Trim())
            .ToList();
    }

    private static bool HasActiveVpnAdapter() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Any(adapter =>
                adapter.OperationalStatus == OperationalStatus.Up
                && (adapter.Name.Contains("vpn", StringComparison.OrdinalIgnoreCase)
                    || adapter.Description.Contains("vpn", StringComparison.OrdinalIgnoreCase)
                    || adapter.Description.Contains("anyconnect", StringComparison.OrdinalIgnoreCase)
                    || adapter.Description.Contains("globalprotect", StringComparison.OrdinalIgnoreCase)
                    || adapter.Description.Contains("forti", StringComparison.OrdinalIgnoreCase)
                    || adapter.Description.Contains("wireguard", StringComparison.OrdinalIgnoreCase)));

    private static async Task<bool> CanReachPortAsync(string host, int port, int timeoutMs)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(timeoutMs);
            await client.ConnectAsync(host, port, cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> HasCertificateErrorAsync(string host)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(2000);
            await client.ConnectAsync(host, 443, cts.Token);

            var hadCertificateError = false;
            using var ssl = new SslStream(client.GetStream(), false, (_, _, _, errors) =>
            {
                hadCertificateError = errors != SslPolicyErrors.None;
                return true;
            });
            await ssl.AuthenticateAsClientAsync(host);
            return hadCertificateError;
        }
        catch
        {
            return false;
        }
    }
}
