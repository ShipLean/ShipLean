using System.Security.Cryptography;
using System.Text;
using ShipLean.Application.Identity;
using Shouldly;

namespace ShipLean.UnitTest.DeviceSessions;

[TestFixture]
public class DeviceSecretTests
{
    [Test]
    public void New_ShouldBePrefixAndFortyThreeBase62Characters()
    {
        var secret = DeviceSecret.New();

        secret.ShouldMatch("^shl_dev_[0-9A-Za-z]{43}$");
        DeviceSecret.HasShape(secret).ShouldBeTrue();
    }

    [Test]
    public void New_WhenCalledTwice_ShouldDiffer()
    {
        DeviceSecret.New().ShouldNotBe(DeviceSecret.New());
    }

    [Test]
    public void TokenPrefix_ShouldBeThePrefixAndTheNextEightCharacters()
    {
        DeviceSecret.TokenPrefix("shl_dev_Vn4cH8sEIrjwAm24iPoRv1IfEtBuDwpe1qj7hLDqlTa").ShouldBe("shl_dev_Vn4cH8sE");
    }

    [Test]
    public void Hash_ShouldBeTheSha256OfTheUtf8Secret()
    {
        var secret = DeviceSecret.New();

        DeviceSecret.Hash(secret).ShouldBe(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
    }

    [Test]
    public void Same_ShouldCompareDigests()
    {
        var secret = DeviceSecret.New();

        DeviceSecret.Same(DeviceSecret.Hash(secret), DeviceSecret.Hash(secret)).ShouldBeTrue();
        DeviceSecret.Same(DeviceSecret.Hash(secret), DeviceSecret.Hash(DeviceSecret.New())).ShouldBeFalse();
    }

    // The compare must not stop at the first differing byte: a digest that differs only in its last byte is refused.
    [Test]
    public void Same_WhenOnlyTheLastByteDiffers_ShouldBeFalse()
    {
        var hash = DeviceSecret.Hash(DeviceSecret.New());
        var other = (byte[])hash.Clone();
        other[^1] ^= 1;

        DeviceSecret.Same(other, hash).ShouldBeFalse();
    }

    [TestCase("shl_dev_Vn4cH8sEIrjwAm24iPoRv1IfEtBuDwpe1qj7hLDqlTa", true)]
    [TestCase("shl_dev_Vn4cH8sEIrjwAm24iPoRv1IfEtBuDwpe1qj7hLDqlT", false)]
    [TestCase("shl_dev_Vn4cH8sEIrjwAm24iPoRv1IfEtBuDwpe1qj7hLDql-a", false)]
    [TestCase("shl_xyz_Vn4cH8sEIrjwAm24iPoRv1IfEtBuDwpe1qj7hLDqlTa", false)]
    [TestCase("", false)]
    public void HasShape_ShouldAcceptOnlyTheSecretShape(string value, bool expected)
    {
        DeviceSecret.HasShape(value).ShouldBe(expected);
    }

    // RFC 7636 appendix B.
    [Test]
    public void ChallengeFor_ShouldBeS256()
    {
        DeviceSecret.ChallengeFor("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk").ShouldBe("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM");
    }

    [TestCase("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", true)]
    [TestCase("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-c", false)]
    [TestCase("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw+cM", false)]
    [TestCase(null, false)]
    public void IsChallenge_ShouldAcceptOnly43Base64UrlCharacters(string? value, bool expected)
    {
        DeviceSecret.IsChallenge(value).ShouldBe(expected);
    }
}
