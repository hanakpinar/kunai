using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Kunai.ArchitectureTests;

public sealed class LayeringTests
{
    private static readonly Architecture Architecture =
        new ArchLoader().LoadAssemblies(typeof(Program).Assembly).Build();

    [Fact]
    public void Common_code_does_not_depend_on_features() =>
        AssertNoViolations(
            Types().That().ResideInNamespaceMatching(@"^Kunai\.Api\.Common(\..*)?$")
                .Should().NotDependOnAny(Types().That().ResideInNamespaceMatching(@"^Kunai\.Api\.Features(\..*)?$")));

    internal static void AssertNoViolations(IArchRule rule)
    {
        var violations = rule.Evaluate(Architecture).Where(result => !result.Passed).Select(result => result.Description).ToList();
        violations.Should().BeEmpty(because: rule.Description);
    }
}
