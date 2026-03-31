using System.ComponentModel;
using System.IO;
using HelpDesk.Application.Interfaces;
using HelpDesk.Domain.Models;
using Newtonsoft.Json;

namespace HelpDesk.Infrastructure.Services;

public sealed class TextSubstitutionService : ITextSubstitutionService, INotifyPropertyChanged
{
    private readonly IReadOnlyDictionary<string, LabelVariantEntry> _entries;
    private bool _simplifiedModeEnabled;

    public TextSubstitutionService()
    {
        _entries = LoadEntries();
    }

    public bool SimplifiedModeEnabled => _simplifiedModeEnabled;

    public string this[string key] => Get(key);

    public string Get(string key, bool? simplifiedModeOverride = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        if (!_entries.TryGetValue(key, out var entry))
            return key;

        var useSimple = simplifiedModeOverride ?? _simplifiedModeEnabled;
        return useSimple ? entry.Simple : entry.Technical;
    }

    public void SetSimplifiedMode(bool enabled)
    {
        if (_simplifiedModeEnabled == enabled)
            return;

        _simplifiedModeEnabled = enabled;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SimplifiedModeEnabled)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static IReadOnlyDictionary<string, LabelVariantEntry> LoadEntries()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Configuration", "label-variants.json");
            if (!File.Exists(path))
                return new Dictionary<string, LabelVariantEntry>(StringComparer.OrdinalIgnoreCase);

            var entries = JsonConvert.DeserializeObject<Dictionary<string, LabelVariantEntry>>(File.ReadAllText(path));
            return entries is null
                ? new Dictionary<string, LabelVariantEntry>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, LabelVariantEntry>(entries, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, LabelVariantEntry>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
