using System.Text.Json.Serialization;
using ShipLean.Contracts.Backups.Dtos;

namespace ShipLean.Contracts.DeviceSessions;

// One row of Signed-in devices. Labels are DisplayDate output in the viewer's zone: LastUsedLabel relative ("2h ago"),
// CreatedLabel a short day ("3 Nov"). Current marks the session the request used. Instants go as UTC with Z.
public sealed record DeviceSessionDto(
    Guid Id,
    string Name,
    string TokenPrefix,
    [property: JsonConverter(typeof(UtcSecondsJsonConverter))] DateTimeOffset CreatedAt,
    string CreatedLabel,
    [property: JsonConverter(typeof(UtcSecondsJsonConverter))] DateTimeOffset LastUsedAt,
    string LastUsedLabel,
    [property: JsonConverter(typeof(UtcSecondsJsonConverter))] DateTimeOffset ExpiresAt,
    bool Current);
