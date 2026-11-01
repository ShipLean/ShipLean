namespace ShipLean.Contracts.Retros;

// Puts a point under a theme before it is inserted. It runs on its own connection and may create a theme; a theme with point_count 0 is never shown.
public interface IRetroThemer
{
    RetroThemeMode Mode { get; }

    Task<RetroThemeResult> AssignAsync(RetroThemeRequest request, CancellationToken cancellationToken);
}
