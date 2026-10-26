/** The chart's y scale: the top is the largest value rounded up to a multiple of 10 (at least 10), with a tick at 0, half and top. */
export function velocityScale(values: number[]): { max: number; ticks: number[] } {
  const largest = Math.max(0, ...values);
  const max = Math.max(10, Math.ceil(largest / 10) * 10);
  return { max, ticks: [0, max / 2, max] };
}
