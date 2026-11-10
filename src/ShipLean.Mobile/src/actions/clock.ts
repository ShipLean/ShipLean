// The device clock, the one place the actions code reads it. The runner and its hooks take `now` as a dependency so
// tests drive time; this is the value the app passes.
export const systemNow = (): Date => new Date();
