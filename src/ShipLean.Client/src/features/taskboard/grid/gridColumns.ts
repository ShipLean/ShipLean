// One column for the row header, then one per Task state.
export const gridColumns = (stateCount: number): string =>
  `minmax(15rem, 20rem) repeat(${stateCount}, minmax(13rem, 1fr))`;
