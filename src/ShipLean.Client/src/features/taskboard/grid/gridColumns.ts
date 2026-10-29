// One column for the row header, then one per Task state.
export const gridColumns = (stateCount: number): string =>
  `minmax(15rem, 20rem) repeat(${stateCount}, minmax(13rem, 1fr))`;

// The least width the grid draws at. The shell's main area is the one scroller (a nested overflow-x would stop the column
// headers sticking to it), so on a narrow screen the grid is wider than main and main scrolls sideways.
export const gridMinWidth = (stateCount: number): string => `calc(15rem + ${stateCount} * 13rem)`;
