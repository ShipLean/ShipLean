// A saved preference. C#: ShipLean.Contracts.Preferences. `json` is the stored value itself (any JSON); the screen that
// writes a key owns its shape. Keys: theme, density, recentProjects, view:<screen>.
export interface UserPreferenceDto {
  key: string;
  json: unknown;
}
