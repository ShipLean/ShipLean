/** The drawn box of one bar or bracket, in the chart's pixels. The edges layer joins these. */
export interface BarBox {
  key: string;
  x0: number;
  x1: number;
  y: number;
  height: number;
}
