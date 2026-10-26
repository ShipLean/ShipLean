using ShipLean.Application.Features.Search;
using ShipLean.Application.Features.Search.Embedding;

namespace ShipLean.WebApi.Tests.Search.Embedding;

// The pinned model, from the folder SHIPLEAN_EMBEDDINGS_MODEL_PATH names. A missing model fails the test and says how to fetch it.
internal static class RealModel
{
    public static string Directory
    {
        // Read directly, not through SearchOptions: the test must fail loudly when the variable is unset (as PostgresServer does for its own).
        get
        {
            var path = Environment.GetEnvironmentVariable(SearchOptions.ModelPathKey);
            var present = !string.IsNullOrWhiteSpace(path) && File.Exists(Path.Combine(path, "model.onnx"));
            present.ShouldBeTrue($"The embedding model is not at {SearchOptions.ModelPathKey}={path ?? "(unset)"}. Run scripts/fetch-embedding-model.sh ~/.cache/shiplean/models/multilingual-e5-small once, then export {SearchOptions.ModelPathKey}=$HOME/.cache/shiplean/models/multilingual-e5-small.");
            return path!;
        }
    }

    public static OnnxEmbedder Load(int threads = 1) => OnnxEmbedder.Load(Directory, new SearchOptions { ModelPath = Directory, Threads = threads });
}
