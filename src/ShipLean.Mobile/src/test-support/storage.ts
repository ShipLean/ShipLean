// The in-memory stores behind the jest mocks of expo-secure-store and expo-sqlite/kv-store. Specs may read them
// (the secret must only ever be in secureValues) but seed the session through renderApp.
export const secureValues = new Map<string, string>();
export const kvValues = new Map<string, string>();
