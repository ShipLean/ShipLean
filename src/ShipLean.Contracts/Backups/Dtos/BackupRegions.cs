namespace ShipLean.Contracts.Backups.Dtos;

// The regions the destination form offers, in the order it lists them. Any other region code can still be typed.
public static class BackupRegions
{
    public static IReadOnlyList<BackupRegionDto> All { get; } =
    [
        new("us-east-1", "US East (N. Virginia)"),
        new("us-west-2", "US West (Oregon)"),
        new("eu-west-1", "Europe (Ireland)"),
        new("eu-central-1", "Europe (Frankfurt)"),
        new("ap-southeast-1", "Asia Pacific (Singapore)"),
        new("ap-southeast-2", "Asia Pacific (Sydney)"),
    ];
}
