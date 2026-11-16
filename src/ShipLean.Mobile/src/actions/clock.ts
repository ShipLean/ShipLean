// The device clock, the one place the actions code reads it. The runner and its hooks take `now` as a dependency so
// tests drive time; this is the value the app passes.
export const systemNow = (): Date => new Date();

/** The ISO 8601 UTC string for an epoch-millisecond instant. The only place the actions code turns a number into a date. */
export const isoAt = (ms: number): string => new Date(ms).toISOString();
