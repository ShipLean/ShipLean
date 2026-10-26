using System.Text.Json.Serialization;

namespace SearchEmbeddings;

// Serialised as the pair [ref, score].
[JsonConverter(typeof(RankedRefConverter))]
public readonly record struct RankedRef(string Ref, double? Score);
