using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Application.Criteria;
using ShipLean.Application.Database;
using ShipLean.Contracts.Criteria;
using ShipLean.Contracts.Persisted;
using ShipLean.TestSupport.Contracts.Criteria;
using ShipLean.TestSupport.Contracts.Users;
using ShipLean.TestSupport.Database;
using Shouldly;

namespace ShipLean.WebApi.Tests.Criteria;

// The real evaluator on the seeded database: each case's item becomes a SHIP work item and the WHERE fragment must agree with
// the case file. One clone for the class, since the cases only add rows.
public sealed class PostgresCriteriaEvaluatorContractTests : CriteriaEvaluatorContract
{
    private readonly SettableCurrentUser _currentUser = new();
    private ClonedDatabase _database = null!;
    private NpgsqlDataSource _dataSource = null!;
    private CriteriaEvaluator _evaluator = null!;

    [OneTimeSetUp]
    public async Task CloneDatabase()
    {
        _database = await ShipLeanDatabase.CloneSeedAsync();
        _dataSource = NpgsqlDataSource.Create(DatabaseServices.PinUtc(_database.ConnectionString));
        _evaluator = new CriteriaEvaluator(_currentUser);
    }

    [OneTimeTearDown]
    public async Task DropDatabase()
    {
        await _dataSource.DisposeAsync();
        await _database.DisposeAsync();
    }

    protected override ICriteriaEvaluator Evaluator => _evaluator;

    protected override IReadOnlySet<CriteriaOp> MatchedOperators { get; } = new HashSet<CriteriaOp>(Enum.GetValues<CriteriaOp>());

    protected override bool SupportsSql => true;

    protected override void ActAs(Guid userId) => _currentUser.Id = userId;

    protected override async Task<bool> MatchesInSqlAsync(CriteriaSql sql, JsonElement item)
    {
        var id = await CriteriaWorkItemRow.InsertAsync(_dataSource, item);
        var parameters = new DynamicParameters();
        parameters.Add("itemId", id);
        foreach (var (name, value) in sql.Parameters)
        {
            parameters.Add(name, value);
        }

        await using var connection = await _dataSource.OpenConnectionAsync();
        return await connection.QuerySingleAsync<bool>($"SELECT EXISTS (SELECT 1 FROM work_item w WHERE w.id = @itemId AND {sql.Where})", parameters);
    }

    [Test]
    public async Task Sql_WhenTwoFragmentsCombined_ShouldRunTogether()
    {
        var priority = new CriteriaJson(CriteriaMatch.All, [new CriteriaClause(FieldIds.Priority, CriteriaOp.Equal, JsonSerializer.SerializeToElement(1))]);
        var tag = new CriteriaJson(CriteriaMatch.All, [new CriteriaClause(FieldIds.Tags, CriteriaOp.Contains, JsonSerializer.SerializeToElement("blocked"))]);
        var id = await CriteriaWorkItemRow.InsertAsync(_dataSource, JsonSerializer.SerializeToElement(new { priority = 1, tags = new[] { "Blocked" } }));
        var first = _evaluator.ToSql(priority, "w");
        var second = _evaluator.ToSql(tag, "w");
        var parameters = new DynamicParameters();
        parameters.Add("itemId", id);
        foreach (var (name, value) in first.Parameters.Concat(second.Parameters))
        {
            parameters.Add(name, value);
        }

        await using var connection = await _dataSource.OpenConnectionAsync();
        var matches = await connection.QuerySingleAsync<bool>($"SELECT EXISTS (SELECT 1 FROM work_item w WHERE w.id = @itemId AND {first.Where} AND {second.Where})", parameters);

        matches.ShouldBeTrue();
    }

    [Test]
    public async Task Sql_WhenFragmentIsNegated_ShouldTreatAnEmptyFieldAsNotMatching()
    {
        var criteria = new CriteriaJson(CriteriaMatch.All, [new CriteriaClause(FieldIds.Severity, CriteriaOp.GreaterThan, JsonSerializer.SerializeToElement(2))]);
        var id = await CriteriaWorkItemRow.InsertAsync(_dataSource, JsonSerializer.SerializeToElement(new { severity = (int?)null }));

        (await RunAsync(id, $"NOT {{0}}", criteria)).ShouldBeTrue();
    }

    [Test]
    public async Task Sql_WhenEpicIsFarAbove_ShouldBeTheNearestEpicAtAnyDepth()
    {
        var epic = SeedWorkItem(92);
        var feature = SeedWorkItem(108);
        var deeperFeature = await CriteriaWorkItemRow.InsertAsync(_dataSource, JsonSerializer.SerializeToElement(new { type = "feature", parent = feature }));
        var deepestFeature = await CriteriaWorkItemRow.InsertAsync(_dataSource, JsonSerializer.SerializeToElement(new { type = "feature", parent = deeperFeature }));
        var story = await CriteriaWorkItemRow.InsertAsync(_dataSource, JsonSerializer.SerializeToElement(new { type = "story", parent = deepestFeature }));
        var underEpic = One(FieldIds.Epic, CriteriaOp.Equal, epic.ToString());

        (await RunAsync(story, "{0}", underEpic)).ShouldBeTrue();
        (await RunAsync(deepestFeature, "{0}", underEpic)).ShouldBeTrue();
        (await RunAsync(deeperFeature, "{0}", underEpic)).ShouldBeTrue();
        (await RunAsync(epic, "{0}", One(FieldIds.Epic, CriteriaOp.IsEmpty, (string?)null))).ShouldBeTrue();
    }

    private static Guid SeedWorkItem(int number) => new($"00000005-0000-4000-8001-{number:D12}");

    private static CriteriaJson One<T>(string field, CriteriaOp op, T value)
        => new(CriteriaMatch.All, [new CriteriaClause(field, op, JsonSerializer.SerializeToElement(value))]);

    // The fragment runs against one row; `shape` wraps it ("NOT {0}") the way a caller would.
    private async Task<bool> RunAsync(Guid id, string shape, CriteriaJson criteria)
    {
        var sql = _evaluator.ToSql(criteria, "w");
        var parameters = new DynamicParameters();
        parameters.Add("itemId", id);
        foreach (var (name, value) in sql.Parameters)
        {
            parameters.Add(name, value);
        }

        await using var connection = await _dataSource.OpenConnectionAsync();
        return await connection.QuerySingleAsync<bool>($"SELECT EXISTS (SELECT 1 FROM work_item w WHERE w.id = @itemId AND {string.Format(shape, sql.Where)})", parameters);
    }
}
