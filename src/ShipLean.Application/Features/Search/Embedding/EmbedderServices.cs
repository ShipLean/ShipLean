using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShipLean.Contracts;
using ShipLean.Contracts.Search;

namespace ShipLean.Application.Features.Search.Embedding;

// Register is public so a test fixture can opt back into the production factory over a host default.
// SHIPLEAN_EMBEDDINGS=off, no model path, or a model that will not load give NoEmbedder with the reason; a bad switch value throws
// from SearchOptions and is not swallowed here.
public sealed class EmbedderServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration) => Register(services);

    public static void Register(IServiceCollection services)
        => services.AddSingleton<IEmbedder>(sp => Create(
            sp.GetRequiredService<SearchOptions>(),
            sp.GetRequiredService<ILoggerFactory>().CreateLogger<EmbedderServices>()));

    private static IEmbedder Create(SearchOptions options, ILogger logger)
    {
        if (!options.Embeddings)
        {
            return new NoEmbedder("Embeddings are switched off (SHIPLEAN_EMBEDDINGS=off).");
        }
        if (options.ModelPath is null)
        {
            logger.LogWarning("{Key} is not set; search stays lexical", SearchOptions.ModelPathKey);
            return new NoEmbedder($"No embedding model: {SearchOptions.ModelPathKey} is not set.");
        }

        try
        {
            var embedder = OnnxEmbedder.Load(options.ModelPath, options);
            logger.LogInformation("Embedding model {Model} loaded from {Path}", embedder.ModelId, options.ModelPath);
            return embedder;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or Microsoft.ML.OnnxRuntime.OnnxRuntimeException or System.Text.Json.JsonException or NotSupportedException or DllNotFoundException or TypeInitializationException or EntryPointNotFoundException or BadImageFormatException)
        {
            logger.LogError(ex, "Embedding model at {Path} did not load; search stays lexical", options.ModelPath);
            return new NoEmbedder($"The embedding model at {options.ModelPath} did not load: {ex.Message}");
        }
    }
}
