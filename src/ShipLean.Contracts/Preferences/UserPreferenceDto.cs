using System.Text.Json;

namespace ShipLean.Contracts.Preferences;

// Json is the stored value itself (any JSON); the screen that writes a preference owns its shape.
public sealed record UserPreferenceDto(string Key, JsonElement Json);
