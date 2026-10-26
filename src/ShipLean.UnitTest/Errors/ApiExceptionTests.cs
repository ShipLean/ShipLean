using ShipLean.Contracts.Errors;
using Shouldly;

namespace ShipLean.UnitTest.Errors;

[TestFixture]
public class ApiExceptionTests
{
    [Test]
    public void Ambiguous_WhenCandidatesGiven_ShouldCarryFieldAndCandidates()
    {
        var error = ApiException.Ambiguous("Two sprints match.", "sprint", ["Sprint 24", "Sprint 24b"]);

        error.Code.ShouldBe(ApiErrorCodes.Ambiguous);
        error.Field.ShouldBe("sprint");
        error.Candidates.ShouldBe(["Sprint 24", "Sprint 24b"]);
    }

    [TestCase(ApiErrorCodes.SlugTaken, "slug")]
    [TestCase(ApiErrorCodes.LinkInvalidUrl, "url")]
    public void Factory_WhenCodeNamesItsField_ShouldSetIt(string code, string field)
    {
        var error = code == ApiErrorCodes.SlugTaken ? ApiException.SlugTaken("Taken.") : ApiException.LinkInvalidUrl("Bad.");

        error.Code.ShouldBe(code);
        error.Field.ShouldBe(field);
    }

    [Test]
    public void ToApiError_WhenFieldSet_ShouldCarryIt()
    {
        var body = ApiException.Validation("Name a project.", "project").ToApiError();

        body.ShouldBe(new ApiError("Name a project.", ApiErrorCodes.Validation, "project"));
    }
}
