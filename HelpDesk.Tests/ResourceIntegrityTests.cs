using System.Resources;
using Xunit;

namespace HelpDesk.Tests;

public sealed class ResourceIntegrityTests
{
    [Fact]
    public void CompiledAssembly_ContainsLogoResourcesAndCompiledXaml()
    {
        var assembly = System.Reflection.Assembly.GetAssembly(typeof(App));
        Assert.NotNull(assembly);

        var resourceName = assembly!.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(".g.resources", StringComparison.OrdinalIgnoreCase));

        Assert.False(string.IsNullOrWhiteSpace(resourceName));

        using var stream = assembly.GetManifestResourceStream(resourceName!);
        Assert.NotNull(stream);
        using var reader = new ResourceReader(stream!);

        var resourceKeys = reader.Cast<System.Collections.DictionaryEntry>()
            .Select(entry => entry.Key?.ToString() ?? string.Empty)
            .ToList();

        Assert.Contains(resourceKeys, key => key.Contains("fixfoxlogo.png", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(resourceKeys, key => key.Contains("fixfoxlogo.ico", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(resourceKeys, key => key.EndsWith(".baml", StringComparison.OrdinalIgnoreCase));
    }
}
