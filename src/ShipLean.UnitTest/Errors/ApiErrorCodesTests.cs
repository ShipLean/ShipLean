using ShipLean.Contracts.Errors;
using Shouldly;

namespace ShipLean.UnitTest.Errors;

[TestFixture]
public class ApiErrorCodesTests
{
    [Test]
    public void StatusFor_ShouldMapEveryCode()
    {
        foreach (var code in ApiErrorCodes.All)
        {
            ApiErrorCodes.StatusFor(code).ShouldBeInRange(400, 599);
        }
    }

    [TestCase(ApiErrorCodes.SlugTaken, 409)]
    [TestCase(ApiErrorCodes.LinkDuplicate, 409)]
    [TestCase(ApiErrorCodes.LinkInvalidUrl, 400)]
    [TestCase(ApiErrorCodes.NotMarkdown, 400)]
    [TestCase(ApiErrorCodes.MarkdownTooLarge, 400)]
    public void StatusFor_WhenANarrowedCode_ShouldMatchItsParent(string code, int status)
    {
        ApiErrorCodes.StatusFor(code).ShouldBe(status);
        ApiErrorCodes.All.ShouldContain(code);
    }
}
