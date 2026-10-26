using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.BoardSettings;

namespace ShipLean.AcceptanceTests.Lanes.BoardSettings.Fields;

// The Fields pane when the stories board stores more extra fields than the limit. The test rewrites the board's
// settings by SQL, so it has the class (and the seeded database) to itself.
[TestFixture]
public class FieldsPaneOverLimitTests : SeededAcceptanceTest
{
    [Test]
    public async Task BoardSettings_WhenElevenExtras_ShouldRefuse()
    {
        // The pane offers fewer than eleven fields for any type, so a document over the limit can only arrive from
        // outside: arrange one in the database and check the pane says so and the save is refused.
        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                """
                UPDATE board SET settings = jsonb_set(settings, '{cardFields,0,extraFields}',
                    (SELECT jsonb_agg('field' || n) FROM generate_series(1, 11) AS n))
                WHERE id = @id
                """,
                connection);
            command.Parameters.AddWithValue("id", ShipLean.TestSupport.SeedIds.Boards.ShipStories);
            (await command.ExecuteNonQueryAsync()).ShouldBe(1);
        }
        var pane = new FieldsPane(Page);
        await pane.OpenAsync("SHIP");

        await Expect(pane.OverLimit).ToContainTextAsync("at most 10 extra fields");
        await Expect(pane.ExtraRows).ToHaveCountAsync(11);
        await Expect(pane.Add).ToBeDisabledAsync();
        await pane.ShowEmpty.CheckAsync();
        await pane.SaveAsync();

        await Expect(pane.SaveError).ToContainTextAsync("at most 10 extra fields");
        await Expect(pane.Pane).ToBeVisibleAsync();
    }
}
