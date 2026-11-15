using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Contracts.Inspection;
using ShipLean.WebApi.Tests.Taskboard;

namespace ShipLean.WebApi.Tests.Inspection.Views;

// Row detail sends the first 40 lines of an output and says there were more.
[TestFixture]
public class RowOutputCapTests : InspectionViewsFixture
{
    private sealed class LongOutputAssembler(IRunAssembler inner) : IRunAssembler
    {
        public static string Lines(int count) => string.Join('\n', Enumerable.Range(1, count).Select(i => $"line {i}"));

        public AssembledRun Assemble(RawRun raw)
        {
            var run = inner.Assemble(raw);
            return run with { Turns = [.. run.Turns.Select(t => t with { Rows = [.. t.Rows.Select(r => r with { Output = Lines(r.Name == "Edit" ? 41 : 40) })] })] };
        }
    }

    protected override Action<IServiceCollection>? ConfigureTestServices
    {
        get
        {
            var baseConfigure = base.ConfigureTestServices;
            return services =>
            {
                baseConfigure?.Invoke(services);
                services.Replace(ServiceDescriptor.Singleton<IRunAssembler>(new LongOutputAssembler(new ShipLean.Application.Features.Inspection.Assembly.RunAssembler())));
            };
        }
    }

    [Test]
    public async Task RowDetail_WhenTheOutputHasMoreThanFortyLines_ShouldCapItAndSayTruncated()
    {
        using var saved = await SetContentAsync(true);
        saved.EnsureSuccessStatusCode();
        var rows = (await Api.GetAsync<RunPageDto>($"{RunsUrl}/{RunId}")).Turns[0].Rows;

        var over = await Api.GetAsync<RunRowDetailDto>($"{RunsUrl}/{RunId}/rows/{rows.First(r => r.Name == "Edit").Id}");
        var exact = await Api.GetAsync<RunRowDetailDto>($"{RunsUrl}/{RunId}/rows/{rows.First(r => r.Name != "Edit").Id}");

        over.OutputTruncated.ShouldBeTrue();
        over.Output!.Split('\n').Length.ShouldBe(40);
        over.Output.ShouldEndWith("line 40");
        exact.OutputTruncated.ShouldBeFalse();
        exact.Output!.Split('\n').Length.ShouldBe(40);
    }
}
