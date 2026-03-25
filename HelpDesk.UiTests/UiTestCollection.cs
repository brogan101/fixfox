using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace HelpDesk.UiTests;

[CollectionDefinition("UI")]
public sealed class UiTestCollection
{
}
