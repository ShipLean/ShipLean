using ShipLean.Application.Database;
using ShipLean.TestSupport.Database;

namespace ShipLean.WebApi.Tests.Harness;

[TestFixture]
public class TemplateKeyTests
{
    private static readonly DateOnly Day = new(2026, 10, 24);

    [Test]
    public void Template_WhenDbOrDateChanges_ShouldRebuild()
    {
        var key = TemplateKey.For("aaaaaaaaaaaa", Day);

        TemplateKey.For("aaaaaaaaaaaa", Day).ShouldBe(key);
        TemplateKey.For("bbbbbbbbbbbb", Day).ShouldNotBe(key);
        TemplateKey.For("aaaaaaaaaaaa", Day.AddDays(1)).ShouldNotBe(key);
    }

    [Test]
    public void Template_WhenNamed_ShouldCarryItsDate()
    {
        TemplateKey.DateOf("shiplean_seed_" + TemplateKey.For("aaaaaaaaaaaa", Day)).ShouldBe(Day);
        TemplateKey.DateOf("shiplean_t_2610241200_abc").ShouldBeNull();
    }

    [Test]
    public void Template_WhenBuiltForToday_ShouldUseTheCurrentScripts()
    {
        ShipLeanDatabase.SeedTemplateName().ShouldNotBe(ShipLeanDatabase.SchemaTemplateName());
        ShipLeanDatabase.SchemaTemplateName().ShouldContain(DatabaseScripts.Hash(includeSeed: false));
        ShipLeanDatabase.SeedTemplateName().ShouldContain(DatabaseScripts.Hash(includeSeed: true));
        TemplateKey.DateOf(ShipLeanDatabase.SchemaTemplateName()).ShouldBe(ShipLeanDatabase.RunDate);
        TemplateKey.DateOf(ShipLeanDatabase.SeedTemplateName()).ShouldBe(ShipLeanDatabase.RunDate);
    }
}
