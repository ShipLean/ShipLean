using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Application.Features.Demos.Brand;
using ShipLean.Application.Features.Demos.Upload;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Demos;

namespace ShipLean.WebApi.Tests.Demos;

// The six seams behind the demo routes, as small fakes that return or throw what a test sets. DemoEndpointShapeTests registers them, so the routes' final
// shapes are pinned over these and never over the stubs. A route whose fake has no reply set fails loudly.
public sealed class DemoEndpointFakes
{
    public SprintDemosFake Demos { get; } = new();

    public DemoSettingsFake Settings { get; } = new();

    public DemoBrandFake Brand { get; } = new();

    public DemoLogoUploadFake LogoUpload { get; } = new();

    public DemoLogoDownloadFake LogoDownload { get; } = new();

    public DemoUploadsFake Uploads { get; } = new();

    public void Install(IServiceCollection services)
    {
        Register<ISprintDemos>(services, Demos);
        Register<IDemoSettings>(services, Settings);
        Register<IDemoBrand>(services, Brand);
        Register<IDemoLogoUpload>(services, LogoUpload);
        Register<IDemoLogoDownload>(services, LogoDownload);
        Register<IDemoUploads>(services, Uploads);
    }

    public void Clear()
    {
        Demos.Clear();
        Settings.Clear();
        Brand.Clear();
        LogoUpload.Clear();
        LogoDownload.Clear();
        Uploads.Clear();
    }

    private static void Register<T>(IServiceCollection services, T fake)
        where T : class
    {
        services.RemoveAll<T>();
        services.AddSingleton(fake);
    }

    // What one method answers: a value built at call time (a stream must be new each time), or an exception.
    public sealed class Reply<T>
    {
        private Func<T>? _answer;

        public void Returns(T value) => _answer = () => value;

        public void Returns(Func<T> answer) => _answer = answer;

        public void Throws(Exception failure) => _answer = () => throw failure;

        public void Clear() => _answer = null;

        public T Take() => (_answer ?? throw new InvalidOperationException("The test set no reply for this call.")).Invoke();
    }

    public sealed class SprintDemosFake : ISprintDemos
    {
        public Reply<SprintDemoDto> Get { get; } = new();

        public Reply<SprintDemoDto> Create { get; } = new();

        public Reply<SprintDemoDto> Retry { get; } = new();

        public void Clear()
        {
            Get.Clear();
            Create.Clear();
            Retry.Clear();
        }

        public Task<SprintDemoDto> GetAsync(Guid sprintId, CancellationToken cancellationToken = default) => Task.FromResult(Get.Take());

        public Task<SprintDemoDto> CreateFromButtonAsync(Guid sprintId, string requestOrigin, CancellationToken cancellationToken = default) => Task.FromResult(Create.Take());

        public Task<SprintDemoDto> RetryYouTubeAsync(Guid sprintId, CancellationToken cancellationToken = default) => Task.FromResult(Retry.Take());

        public Task<SprintDemoHandover> CreateForAgentAsync(Guid sprintId, string baseUrl, string agentStamp, Guid? authorizationId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("No demo route starts an agent run for an agent.");

        public Task StartAutomaticAsync(Guid projectId, Guid sprintId, Guid closedBy, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("No demo route starts an automatic run.");
    }

    public sealed class DemoSettingsFake : IDemoSettings
    {
        public Reply<ProjectDemoSettingsDto> Get { get; } = new();

        public Reply<ProjectDemoSettingsDto> Save { get; } = new();

        public void Clear()
        {
            Get.Clear();
            Save.Clear();
        }

        public Task<ProjectDemoSettingsDto> GetAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult(Get.Take());

        public Task<ProjectDemoSettingsDto> SaveAsync(Guid projectId, ProjectDemoSettingsPut settings, CancellationToken cancellationToken = default) => Task.FromResult(Save.Take());
    }

    public sealed class DemoBrandFake : IDemoBrand
    {
        public Reply<ProjectDemoBrandDto> Read { get; } = new();

        public Reply<BlobContent> OpenLogo { get; } = new();

        public Reply<ProjectDemoBrandDto> SaveTokens { get; } = new();

        public Reply<ProjectDemoBrandDto> RemoveLogo { get; } = new();

        public void Clear()
        {
            Read.Clear();
            OpenLogo.Clear();
            SaveTokens.Clear();
            RemoveLogo.Clear();
        }

        public Task<ProjectDemoBrandDto> ReadAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult(Read.Take());

        public Task<BlobContent> OpenLogoAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult(OpenLogo.Take());

        public Task<ProjectDemoBrandDto> SaveTokensAsync(Guid projectId, string? tokens, CancellationToken cancellationToken = default) => Task.FromResult(SaveTokens.Take());

        public Task<ProjectDemoBrandDto> RemoveLogoAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult(RemoveLogo.Take());
    }

    public sealed class DemoLogoUploadFake : IDemoLogoUpload
    {
        public Reply<ProjectDemoBrandDto> Replace { get; } = new();

        public void Clear() => Replace.Clear();

        public Task<ProjectDemoBrandDto> ReplaceAsync(Guid projectId, HttpRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Replace.Take());
    }

    public sealed class DemoLogoDownloadFake : IDemoLogoDownload
    {
        public Reply<BlobContent> Open { get; } = new();

        public string? LastToken { get; private set; }

        public void Clear()
        {
            Open.Clear();
            LastToken = null;
        }

        public Task<BlobContent> OpenAsync(string token, HttpRequest request, CancellationToken cancellationToken = default)
        {
            LastToken = token;
            return Task.FromResult(Open.Take());
        }
    }

    public sealed class DemoUploadsFake : IDemoUploads
    {
        public Reply<DemoUploadedDto> Upload { get; } = new();

        public string? LastToken { get; private set; }

        public void Clear()
        {
            Upload.Clear();
            LastToken = null;
        }

        public Task<DemoUploadedDto> UploadAsync(string token, HttpRequest request, CancellationToken cancellationToken = default)
        {
            LastToken = token;
            return Task.FromResult(Upload.Take());
        }
    }
}
