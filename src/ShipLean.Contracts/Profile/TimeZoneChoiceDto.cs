namespace ShipLean.Contracts.Profile;

// One time zone the Profile page offers: its IANA id and a label with its offset now ("Asia/Bangkok (UTC+07:00)").
public sealed record TimeZoneChoiceDto(string Id, string Label);
