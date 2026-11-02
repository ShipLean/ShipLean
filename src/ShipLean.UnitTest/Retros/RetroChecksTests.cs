using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros;

namespace ShipLean.UnitTest.Retros;

[TestFixture]
public class RetroChecksTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();

    [TestCase("w6", RetroRange.Weeks6)]
    [TestCase("w12", RetroRange.Weeks12)]
    [TestCase("3", RetroRange.Last3)]
    [TestCase(null, RetroRange.Last6)]
    public void Range_WhenKnown_ShouldParse(string? range, RetroRange expected)
    {
        RetroChecks.Range(range).ShouldBe(expected);
    }

    [Test]
    public void Range_WhenUnknown_ShouldBeValidation()
    {
        Field(() => RetroChecks.Range("w3")).ShouldBe("range");
    }

    [Test]
    public void ActionNew_WhenValid_ShouldPass()
    {
        Should.NotThrow(() => RetroChecks.ActionNew(new RetroActionNew("Fix it", RetroFixType.Other, [A, B], null, "Flaky CI")));
        Should.NotThrow(() => RetroChecks.ActionNew(new RetroActionNew("Fix it", RetroFixType.Skill, [A], Guid.NewGuid(), null)));
    }

    [Test]
    public void ActionNew_WhenBlankTitle_ShouldBeValidationOnTitle()
    {
        Field(() => RetroChecks.ActionNew(new RetroActionNew("  ", RetroFixType.Test, [A], null, null))).ShouldBe("title");
    }

    [Test]
    public void ActionNew_WhenNoPoints_ShouldBeValidationOnPointIds()
    {
        Field(() => RetroChecks.ActionNew(new RetroActionNew("Fix", RetroFixType.Test, [], null, null))).ShouldBe("pointIds");
    }

    [Test]
    public void ActionNew_WhenDuplicatePoints_ShouldBeValidationOnPointIds()
    {
        Field(() => RetroChecks.ActionNew(new RetroActionNew("Fix", RetroFixType.Test, [A, A], null, null))).ShouldBe("pointIds");
    }

    [Test]
    public void ActionNew_WhenThemeIdAndName_ShouldBeValidationOnThemeId()
    {
        Field(() => RetroChecks.ActionNew(new RetroActionNew("Fix", RetroFixType.Test, [A], A, "Name"))).ShouldBe("themeId");
    }

    [Test]
    public void ActionNew_WhenUnknownFixType_ShouldBeValidationOnFixType()
    {
        Field(() => RetroChecks.ActionNew(new RetroActionNew("Fix", (RetroFixType)99, [A], null, null))).ShouldBe("fixType");
    }

    [Test]
    public void ActionAddPoints_WhenEmptyOrDuplicate_ShouldBeValidationOnPointIds()
    {
        Field(() => RetroChecks.ActionAddPoints(new RetroActionAddPoints([]))).ShouldBe("pointIds");
        Field(() => RetroChecks.ActionAddPoints(new RetroActionAddPoints([A, B, A]))).ShouldBe("pointIds");
        Should.NotThrow(() => RetroChecks.ActionAddPoints(new RetroActionAddPoints([A, B])));
    }

    [Test]
    public void ActionPut_WhenThemeIdAndName_ShouldBeValidationOnThemeId()
    {
        Field(() => RetroChecks.ActionPut(new RetroActionPut(RetroFixType.Guide, A, "Name"))).ShouldBe("themeId");
    }

    [Test]
    public void ActionPut_WhenBothThemeFieldsNullOrFixTypeNull_ShouldPass()
    {
        Should.NotThrow(() => RetroChecks.ActionPut(new RetroActionPut(null, null, null)));
        Should.NotThrow(() => RetroChecks.ActionPut(new RetroActionPut(RetroFixType.Product, null, "Theme")));
    }

    [Test]
    public void NotNeededPut_WhenBlankReason_ShouldBeValidationOnReason()
    {
        Field(() => RetroChecks.NotNeededPut(new RetroNotNeededPut(" "))).ShouldBe("reason");
        Should.NotThrow(() => RetroChecks.NotNeededPut(new RetroNotNeededPut("One-off.")));
    }

    private static string? Field(Action check)
    {
        var error = Should.Throw<ApiException>(check);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        return error.Field;
    }
}
