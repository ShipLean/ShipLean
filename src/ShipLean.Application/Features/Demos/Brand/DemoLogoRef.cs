namespace ShipLean.Application.Features.Demos.Brand;

// A project's stored logo as the settings panel shows it: the version-keyed address and the stored type.
public sealed record DemoLogoRef(string Url, string ContentType);
