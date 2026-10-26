using ShipLean.Contracts.Search;
using ShipLean.TestSupport.Contracts.Search;

namespace ShipLean.WebApi.Tests.Search.Embedding;

// The shared embedder contract against the real model.
// Runs alone: the real model is ~700 MB resident while loaded, and CI's 2 GB runners can't hold several next to the parallel hosts.
[TestFixture]
[NonParallelizable]
public class OnnxEmbedderContractTests : EmbedderContract
{
    protected override IEmbedder CreateEmbedder() => RealModel.Load();
}
