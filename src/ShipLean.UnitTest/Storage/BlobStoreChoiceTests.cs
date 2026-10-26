using Microsoft.Extensions.Configuration;
using ShipLean.Application.Storage;

namespace ShipLean.UnitTest.Storage;

[TestFixture]
public class BlobStoreChoiceTests
{
    private const string S3 = "bucket=b;region=r;accessKey=AK;secretKey=TOPSECRET";
    private const string Db = "Host=db;Database=blobs;Username=u;Password=HUSH";

    [Test]
    public void From_WhenNothingIsSet_ShouldUseDataBlobsUnderTheContentRoot()
    {
        var choice = Choose();

        choice.Kind.ShouldBe(BlobStoreKind.File);
        choice.Description.ShouldBe(Path.GetFullPath("/app/data/blobs"));
        choice.Warning.ShouldBeNull();
    }

    [Test]
    public void From_WhenOnlyTheRootIsSet_ShouldUseThatFolder()
    {
        var choice = Choose((BlobStoreServices.RootKey, "/data/blobs"));

        choice.Kind.ShouldBe(BlobStoreKind.File);
        choice.Setting.ShouldBe(BlobStoreServices.RootKey);
        choice.Description.ShouldBe("/data/blobs");
    }

    [Test]
    public void From_WhenTheDbConnectionIsSet_ShouldUsePostgresWithoutThePassword()
    {
        var choice = Choose((BlobStoreServices.DbConnectionKey, Db), (BlobStoreServices.RootKey, "/data/blobs"));

        choice.Kind.ShouldBe(BlobStoreKind.Postgres);
        choice.Setting.ShouldBe(BlobStoreServices.DbConnectionKey);
        choice.Description.ShouldContain("Database=blobs");
        choice.Description.ShouldNotContain("HUSH");
        choice.Warning.ShouldBeNull();
    }

    [Test]
    public void From_WhenTheS3ConnectionIsSet_ShouldUseS3WithoutTheSecretKey()
    {
        var choice = Choose((BlobStoreServices.S3ConnectionKey, S3), (BlobStoreServices.RootKey, "/data/blobs"));

        choice.Kind.ShouldBe(BlobStoreKind.S3);
        choice.Setting.ShouldBe(BlobStoreServices.S3ConnectionKey);
        choice.Description.ShouldContain("bucket=b");
        choice.Description.ShouldNotContain("TOPSECRET");
        choice.Warning.ShouldBeNull();
    }

    [Test]
    public void From_WhenS3AndDbAreBothSet_ShouldUseS3AndWarn()
    {
        var choice = Choose((BlobStoreServices.S3ConnectionKey, S3), (BlobStoreServices.DbConnectionKey, Db));

        choice.Kind.ShouldBe(BlobStoreKind.S3);
        choice.Warning.ShouldNotBeNull().ShouldContain("S3 wins");
        choice.Warning.ShouldNotContain("HUSH");
    }

    [Test]
    public void From_WhenASettingIsBlank_ShouldTreatItAsUnset()
    {
        Choose((BlobStoreServices.S3ConnectionKey, " "), (BlobStoreServices.DbConnectionKey, "")).Kind.ShouldBe(BlobStoreKind.File);
    }

    private static BlobStoreChoice Choose(params (string Key, string Value)[] settings)
        => BlobStoreChoice.From(
            new ConfigurationBuilder().AddInMemoryCollection(settings.Select(s => KeyValuePair.Create(s.Key, (string?)s.Value))).Build(),
            "/app");
}
