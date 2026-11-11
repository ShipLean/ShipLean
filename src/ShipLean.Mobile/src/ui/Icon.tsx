import Svg, { Circle, Path, Rect } from 'react-native-svg';
import { useTheme } from '../theme';
import { iconId } from './testIds';

// Shapes of one icon, ported from the mock kit's own paths (design/mobileapp/app.js, object P), 24x24 viewBox.
type Shape =
  | { kind: 'path'; d: string; filled?: true }
  | { kind: 'circle'; cx: number; cy: number; r: number }
  | { kind: 'rect'; x: number; y: number; w: number; h: number; rx: number };

const p = (d: string): Shape => ({ kind: 'path', d });
const c = (cx: number, cy: number, r: number): Shape => ({ kind: 'circle', cx, cy, r });
const r = (x: number, y: number, w: number, h: number, rx: number): Shape => ({
  kind: 'rect',
  x,
  y,
  w,
  h,
  rx,
});

const SHAPES = {
  mic: [r(9, 3, 6, 11, 3), p('M5 11a7 7 0 0 0 14 0M12 18v3')],
  back: [p('M15 5l-7 7 7 7')],
  chevron: [p('M9 5l7 7-7 7')],
  down: [p('M6 9l6 6 6-6')],
  search: [c(11, 11, 6.5), p('M20 20l-4.2-4.2')],
  more: [c(5, 12, 1.3), c(12, 12, 1.3), c(19, 12, 1.3)],
  check: [p('M5 12.5l4.5 4.5L19 7.5')],
  x: [p('M6 6l12 12M18 6L6 18')],
  undo: [p('M9 14L4 9l5-5'), p('M4 9h10a6 6 0 0 1 0 12h-3')],
  play: [{ kind: 'path', d: 'M8 5.5v13l10-6.5z', filled: true }],
  pause: [p('M8 5v14M16 5v14')],
  up: [p('M12 19V5M6 11l6-6 6 6')],
  arrowdown: [p('M12 5v14M6 13l6 6 6-6')],
  rank: [p('M7 4v16M4 7l3-3 3 3M17 20V4M14 17l3 3 3-3')],
  flag: [p('M5 21V4M5 4h11l-2 4 2 4H5')],
  alert: [p('M12 4l9 16H3z'), p('M12 10v4M12 17.2v.1')],
  note: [p('M5 4h14v12l-4 4H5z'), p('M15 20v-4h4M8 9h8M8 13h5')],
  message: [p('M4 5h16v11H9l-5 4z')],
  inbox: [p('M3 13l3-8h12l3 8v6H3z'), p('M3 13h5l1 3h6l1-3h5')],
  list: [p('M8 6h12M8 12h12M8 18h12'), c(4, 6, 1), c(4, 12, 1), c(4, 18, 1)],
  board: [r(3, 4, 5, 16, 1.5), r(9.5, 4, 5, 11, 1.5), r(16, 4, 5, 7, 1.5)],
  retro: [p('M4 12a8 8 0 1 0 2.4-5.7'), p('M4 4v4h4'), p('M12 8v4l3 2')],
  user: [c(12, 8, 4), p('M4 20a8 8 0 0 1 16 0')],
  spark: [p('M12 3l1.8 5.2L19 10l-5.2 1.8L12 17l-1.8-5.2L5 10l5.2-1.8z')],
  filter: [p('M4 5h16l-6 7v6l-4 2v-8z')],
  plus: [p('M12 5v14M5 12h14')],
  keyboard: [r(3, 6, 18, 12, 2), p('M7 10h.01M11 10h.01M15 10h.01M7 14h10')],
  thumb: [
    p('M7 11v9H4v-9zM7 11l4-7a2 2 0 0 1 3 2l-1 4h6a2 2 0 0 1 2 2.3l-1.2 6A2 2 0 0 1 17.8 20H7'),
  ],
  clock: [c(12, 12, 8), p('M12 8v4l3 2')],
  download: [p('M12 4v11M7 10l5 5 5-5M5 20h14')],
  eye: [p('M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12z'), c(12, 12, 3)],
  link: [
    p(
      'M10 14a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1 1M14 10a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1-1',
    ),
  ],
  speaker: [p('M4 9h4l5-4v14l-5-4H4z'), p('M16 9a4 4 0 0 1 0 6M18.5 6.5a8 8 0 0 1 0 11')],
} as const satisfies Record<string, readonly Shape[]>;

export type IconName = keyof typeof SHAPES;
export const ICON_NAMES = Object.keys(SHAPES) as IconName[];

export interface IconProps {
  name: IconName;
  size?: 16 | 20 | 24;
  // Defaults to the theme's text colour (the kit's currentColor).
  color?: string;
}

// A stroke icon (app.css svg.i): 1.8 wide, round caps and joins, 20 points unless told otherwise.
export function Icon({ name, size = 20, color }: IconProps) {
  const theme = useTheme();
  const ink = color ?? theme.colors.text;
  return (
    <Svg
      testID={iconId(name)}
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke={ink}
      strokeWidth={1.8}
      strokeLinecap="round"
      strokeLinejoin="round"
      accessibilityElementsHidden
      importantForAccessibility="no-hide-descendants"
    >
      {SHAPES[name].map((shape, i) => {
        const key = `${name}-${i}`;
        if (shape.kind === 'circle')
          return <Circle key={key} cx={shape.cx} cy={shape.cy} r={shape.r} />;
        if (shape.kind === 'rect')
          return (
            <Rect
              key={key}
              x={shape.x}
              y={shape.y}
              width={shape.w}
              height={shape.h}
              rx={shape.rx}
            />
          );
        return <Path key={key} d={shape.d} fill={'filled' in shape ? ink : undefined} />;
      })}
    </Svg>
  );
}
