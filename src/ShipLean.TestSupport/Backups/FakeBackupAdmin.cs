using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShipLean.Application.Features.Backups.Admin;
using ShipLean.Contracts.Backups.Dtos;

namespace ShipLean.TestSupport.Backups;

// IBackupAdmin for the API shape tests, which register it in ConfigureTestServices. Each answer comes from the delegate
// the test passes in: return a DTO (Pinned reads the pinned examples) or throw the ApiException the route maps. Every
// call is recorded, so a test can check what the controller handed over.
public sealed class FakeBackupAdmin(
    Func<BackupSettingsDto> get,
    Func<BackupSaveDto, BackupSettingsDto> save,
    Func<BackupDestinationSaveDto, BackupTestResultDto> test,
    Func<string, CronPreviewDto> previewCron,
    Func<BackupRunDto> runNow) : IBackupAdmin
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private readonly ConcurrentQueue<(BackupSaveDto Body, Guid UserId)> _saves = new();
    private readonly ConcurrentQueue<BackupDestinationSaveDto> _tests = new();
    private readonly ConcurrentQueue<string> _previews = new();
    private readonly ConcurrentQueue<Guid> _runs = new();

    public IReadOnlyList<(BackupSaveDto Body, Guid UserId)> Saves => [.. _saves];

    public IReadOnlyList<BackupDestinationSaveDto> Tests => [.. _tests];

    public IReadOnlyList<string> Previews => [.. _previews];

    public IReadOnlyList<Guid> Runs => [.. _runs];

    // One example of a pinned backup-*.json file, read with the API's options ("backup-run.json", "running").
    public static T Pinned<T>(string file, string example)
    {
        var path = Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "PinnedShapes", file);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty(example).Deserialize<T>(Options)
            ?? throw new InvalidOperationException($"{file} has a null {example}.");
    }

    public Task<BackupSettingsDto> GetAsync(CancellationToken cancellationToken) => Task.FromResult(get());

    public Task<BackupSettingsDto> SaveAsync(BackupSaveDto body, Guid userId, CancellationToken cancellationToken)
    {
        _saves.Enqueue((body, userId));
        return Task.FromResult(save(body));
    }

    public Task<BackupTestResultDto> TestAsync(BackupDestinationSaveDto destination, CancellationToken cancellationToken)
    {
        _tests.Enqueue(destination);
        return Task.FromResult(test(destination));
    }

    public Task<CronPreviewDto> PreviewCronAsync(string cron, CancellationToken cancellationToken)
    {
        _previews.Enqueue(cron);
        return Task.FromResult(previewCron(cron));
    }

    public Task<BackupRunDto> RunNowAsync(Guid userId, CancellationToken cancellationToken)
    {
        _runs.Enqueue(userId);
        return Task.FromResult(runNow());
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}
