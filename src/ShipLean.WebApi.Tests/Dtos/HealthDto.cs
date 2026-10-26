namespace ShipLean.WebApi.Tests.Dtos;

public record HealthDto(string Status, string Version, HealthSearchDto? Search = null);
