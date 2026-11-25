namespace ShipLean.Contracts.Demos;

// A wait between two items of the Feature: Kind is "depends" (From had to finish before To started) or "blocked".
public sealed record SprintDemoWait(string FromKey, string FromTitle, string ToKey, string ToTitle, string Kind, double WaitHours, string Label, bool OnCriticalPath);
