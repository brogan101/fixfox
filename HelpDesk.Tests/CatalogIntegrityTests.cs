using HelpDesk.Domain.Enums;
using HelpDesk.Infrastructure.Fixes;
using Xunit;

namespace HelpDesk.Tests;

public sealed class CatalogIntegrityTests
{
    [Fact]
    public void Every_fix_entry_has_required_metadata_and_valid_category()
    {
        var catalog = new FixCatalogService();
        var categories = catalog.Categories.ToList();
        var categoryNames = categories.Select(category => category.Title).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var fixes = categories.SelectMany(category => category.Fixes).ToList();

        Assert.NotEmpty(fixes);
        Assert.True(fixes.Count > 350, $"Expected fix count > 350 but found {fixes.Count}.");

        var duplicateIds = fixes
            .GroupBy(fix => fix.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        Assert.Empty(duplicateIds);

        foreach (var fix in fixes)
        {
            Assert.False(string.IsNullOrWhiteSpace(fix.Id), "Fix Id should not be blank.");
            Assert.False(string.IsNullOrWhiteSpace(fix.Title), $"Fix '{fix.Id}' should have a title.");
            Assert.False(string.IsNullOrWhiteSpace(fix.Category), $"Fix '{fix.Id}' should have a category.");
            Assert.False(string.IsNullOrWhiteSpace(fix.Description), $"Fix '{fix.Id}' should have a description.");
            Assert.Contains(fix.Category, categoryNames);
            Assert.True(fix.EstimatedDurationSeconds > 0, $"Fix '{fix.Id}' should have a positive estimated duration.");
            Assert.True(fix.Steps.Count > 0 || !string.IsNullOrWhiteSpace(fix.Script), $"Fix '{fix.Id}' should have at least one executable step or script.");
            if (fix.Type == FixType.Guided)
                Assert.True(fix.Steps.Count > 0, $"Guided fix '{fix.Id}' should contain guided steps.");
        }
    }
}
