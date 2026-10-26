namespace ShipLean.TestSupport.Http;

// What StubHttpServer answers for one path.
public sealed record StubResponse(int Status, byte[] Body, string ContentType, IReadOnlyDictionary<string, string>? Headers = null);
