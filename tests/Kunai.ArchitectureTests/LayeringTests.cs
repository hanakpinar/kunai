using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
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

    // Features depend in one direction: Profiles -> Library -> Catalog -> Identity, with
    // Moderation beside Library. The rules below keep a slice from reaching back up that chain.

    [Fact]
    public void Catalog_does_not_depend_on_the_slices_built_on_it() =>
        AssertNoViolations(
            Slice("Catalog").Should().NotDependOnAny(Slice("Library", "Moderation", "Profiles")));

    [Fact]
    public void Library_does_not_depend_on_profiles_or_moderation() =>
        AssertNoViolations(
            Slice("Library").Should().NotDependOnAny(Slice("Moderation", "Profiles")));

    [Fact]
    public void Moderation_does_not_depend_on_library_or_profiles() =>
        AssertNoViolations(
            Slice("Moderation").Should().NotDependOnAny(Slice("Library", "Profiles")));

    // Registration gives each new user a default avatar, so Identity may use Profiles.Avatars,
    // and nothing else in another feature. The next rule keeps that edge one-way.
    [Fact]
    public void Identity_depends_on_no_other_feature_except_default_avatars() =>
        AssertNoViolations(
            Slice("Identity").Should().NotDependOnAny(
                Types().That().ResideInNamespaceMatching(
                    @"^Kunai\.Api\.Features\.(Catalog|Library|Moderation|Profiles(?!\.Avatars(\.|$)))(\..*)?$")));

    [Fact]
    public void Avatars_do_not_depend_on_identity() =>
        AssertNoViolations(
            Types().That().ResideInNamespaceMatching(@"^Kunai\.Api\.Features\.Profiles\.Avatars(\..*)?$")
                .Should().NotDependOnAny(Slice("Identity")));

    private static GivenTypesConjunction Slice(params string[] features) =>
        Types().That().ResideInNamespaceMatching($@"^Kunai\.Api\.Features\.({string.Join('|', features)})(\..*)?$");

    internal static void AssertNoViolations(IArchRule rule)
    {
        var violations = rule.Evaluate(Architecture).Where(result => !result.Passed).Select(result => result.Description).ToList();
        violations.Should().BeEmpty(because: rule.Description);
    }
}
