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

    public ConcurrentQueue<(BackupSaveDto Body, Guid UserId)> Saves { get; } = new();

    public ConcurrentQueue<BackupDestinationSaveDto> Tests { get; } = new();

    public ConcurrentQueue<string> Previews { get; } = new();

    public ConcurrentQueue<Guid> Runs { get; } = new();

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
        Saves.Enqueue((body, userId));
        return Task.FromResult(save(body));
    }

    public Task<BackupTestResultDto> TestAsync(BackupDestinationSaveDto destination, CancellationToken cancellationToken)
    {
        Tests.Enqueue(destination);
        return Task.FromResult(test(destination));
    }

    public Task<CronPreviewDto> PreviewCronAsync(string cron, CancellationToken cancellationToken)
    {
        Previews.Enqueue(cron);
        return Task.FromResult(previewCron(cron));
    }

    public Task<BackupRunDto> RunNowAsync(Guid userId, CancellationToken cancellationToken)
    {
        Runs.Enqueue(userId);
        return Task.FromResult(runNow());
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}
