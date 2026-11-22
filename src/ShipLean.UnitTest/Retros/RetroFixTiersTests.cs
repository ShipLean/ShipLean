using ShipLean.Application.Features.Retros.People;
using ShipLean.Contracts.Retros;

namespace ShipLean.UnitTest.Retros;

[TestFixture]
public class RetroFixTiersTests
{
    [TestCase(RetroFixType.Fix, 1, "fix:fix")]
    [TestCase(RetroFixType.Structure, 2, "fix:structure")]
    [TestCase(RetroFixType.Analyzer, 3, "fix:analyzer")]
    [TestCase(RetroFixType.Test, 4, "fix:test")]
    [TestCase(RetroFixType.Skill, 4, "fix:skill")]
    [TestCase(RetroFixType.Guide, 4, "fix:guide")]
    [TestCase(RetroFixType.Product, null, null)]
    [TestCase(RetroFixType.Other, null, null)]
    public void Tier_ForEachFixType_ShouldMapAsC4(RetroFixType fixType, int? priority, string? tag)
    {
        RetroFixTiers.Priority(fixType).ShouldBe(priority);
        RetroFixTiers.Tag(fixType).ShouldBe(tag);
    }

    [Test]
    public void Changes_WhenNoTier_ShouldBeNull()
    {
        RetroFixTiers.Changes(null).ShouldBeNull();
        RetroFixTiers.Changes(RetroFixType.Other).ShouldBeNull();
    }
}
