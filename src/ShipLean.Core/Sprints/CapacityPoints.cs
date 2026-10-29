namespace ShipLean.Core.Sprints;

// A member's capacity in points: velocity / members * days / workingDays, rounded to 2 decimals (half away from zero) and
// capped at Largest. No working days gives 0. Multiplied out before dividing so the decimal result is exact.
public static class CapacityPoints
{
    public const decimal Largest = 9999.99m;

    public static decimal For(decimal velocity, int members, decimal days, int workingDays)
    {
        if (workingDays <= 0 || members <= 0)
        {
            return 0m;
        }

        var points = decimal.Round(velocity * days / ((decimal)members * workingDays), 2, MidpointRounding.AwayFromZero);
        return Math.Min(Largest, points);
    }
}
