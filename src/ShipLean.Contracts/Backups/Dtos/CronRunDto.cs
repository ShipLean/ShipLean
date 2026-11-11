using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Backups.Dtos;

// One upcoming run of a schedule, with its server label ("2026-10-25 02:00 UTC").
public sealed record CronRunDto([property: JsonConverter(typeof(UtcSecondsJsonConverter))] DateTimeOffset At, string Label);
