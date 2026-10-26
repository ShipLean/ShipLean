using Microsoft.AspNetCore.Http;
using ShipLean.Application.Requests;
using ShipLean.Contracts;

namespace ShipLean.UnitTest.Application;

[TestFixture]
public class RequestOriginTests
{
    [Test]
    public void ClientHeader_ShouldBeTheShipLeanClientHeader()
    {
        ClientHeader.Name.ShouldBe("X-ShipLean-Client");
    }

    [Test]
    public void ClientId_WhenTheRequestCarriesTheHeader_ShouldBeItsValue()
    {
        var origin = OriginOf(new DefaultHttpContext { Request = { Headers = { [ClientHeader.Name] = "tab-7f3a" } } });

        origin.ClientId.ShouldBe("tab-7f3a");
    }

    [Test]
    public void ClientId_WhenTheHeaderIsMissing_ShouldBeNull()
    {
        OriginOf(new DefaultHttpContext()).ClientId.ShouldBeNull();
    }

    [TestCase("")]
    [TestCase("   ")]
    public void ClientId_WhenTheHeaderIsBlank_ShouldBeNull(string value)
    {
        var origin = OriginOf(new DefaultHttpContext { Request = { Headers = { [ClientHeader.Name] = value } } });

        origin.ClientId.ShouldBeNull();
    }

    [Test]
    public void ClientId_WhenThereIsNoRequest_ShouldBeNull()
    {
        OriginOf(null).ClientId.ShouldBeNull();
    }

    [Test]
    public void ClientId_WhenTheRequestChanges_ShouldFollowTheCurrentRequest()
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { Request = { Headers = { [ClientHeader.Name] = "first" } } },
        };
        var origin = new RequestOrigin(() => accessor.HttpContext);
        origin.ClientId.ShouldBe("first");

        accessor.HttpContext = new DefaultHttpContext { Request = { Headers = { [ClientHeader.Name] = "second" } } };

        origin.ClientId.ShouldBe("second");
    }

    private static RequestOrigin OriginOf(HttpContext? context) => new(() => context);
}
