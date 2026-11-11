import type { ReactNode } from 'react';
import Svg, { Circle, Path, Rect } from 'react-native-svg';

// The line icons of design/mobileapp (app.js, the `P` paths): a 24 × 24 view box, drawn with the stroke only, round
// caps and joins (app.css svg.i). Add an icon by copying its path from app.js.
const PATHS = {
  inbox: (
    <>
      <Path d="M3 13l3-8h12l3 8v6H3z" />
      <Path d="M3 13h5l1 3h6l1-3h5" />
    </>
  ),
  list: (
    <>
      <Path d="M8 6h12M8 12h12M8 18h12" />
      <Circle cx={4} cy={6} r={1} />
      <Circle cx={4} cy={12} r={1} />
      <Circle cx={4} cy={18} r={1} />
    </>
  ),
  board: (
    <>
      <Rect x={3} y={4} width={5} height={16} rx={1.5} />
      <Rect x={9.5} y={4} width={5} height={11} rx={1.5} />
      <Rect x={16} y={4} width={5} height={7} rx={1.5} />
    </>
  ),
  retro: (
    <>
      <Path d="M4 12a8 8 0 1 0 2.4-5.7" />
      <Path d="M4 4v4h4" />
      <Path d="M12 8v4l3 2" />
    </>
  ),
  mic: (
    <>
      <Rect x={9} y={3} width={6} height={11} rx={3} />
      <Path d="M5 11a7 7 0 0 0 14 0M12 18v3" />
    </>
  ),
} satisfies Record<string, ReactNode>;

export type IconName = keyof typeof PATHS;

export interface IconProps {
  name: IconName;
  color: string;
  // svg.i: 20; .mic svg.i: 28.
  size?: number;
  // svg.i: 1.8; .mic svg.i: 2.
  strokeWidth?: number;
  testID?: string;
}

export function Icon({ name, color, size = 20, strokeWidth = 1.8, testID }: IconProps) {
  return (
    <Svg
      testID={testID}
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke={color}
      strokeWidth={strokeWidth}
      strokeLinecap="round"
      strokeLinejoin="round"
      accessible={false}
      importantForAccessibility="no"
    >
      {PATHS[name]}
    </Svg>
  );
}
