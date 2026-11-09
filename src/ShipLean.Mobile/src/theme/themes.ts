// The app's theme values, light and dark (K6 shape).
// TODO: hand-copied from design/tokens.json; replaced by tokens.generated.ts from scripts/generate-theme.mjs.
export type Scheme = 'light' | 'dark';

export interface Colors {
  bg: string;
  surface: string;
  surface2: string;
  border: string;
  text: string;
  muted: string;
  accent: string;
  accentFg: string;
  accentSoft: string;
  ok: string;
  warn: string;
  info: string;
  danger: string;
  purple: string;
}

export type StateCategory = 'proposed' | 'inProgress' | 'resolved' | 'completed' | 'removed';

export interface Theme {
  scheme: Scheme;
  colors: Colors;
  // Sizes in points with the touch overrides applied (input text is 16 so iOS never zooms).
  font: {
    size: {
      xxs: number;
      xs: number;
      sm: number;
      base: number;
      md: number;
      lg: number;
      input: number;
    };
    weight: { regular: '400'; medium: '500'; semibold: '600'; bold: '700' };
  };
  // Touch sizes: the top bar's height, a control's minimum height, a row's minimum height.
  touch: { bar: number; control: number; row: number };
  space: { 1: number; 2: number; 3: number; 4: number; 5: number };
  radius: { xs: number; sm: number; md: number; lg: number };
  stateCategory: Record<StateCategory, string>;
}

const light: Colors = {
  bg: '#e8eef7',
  surface: '#f7f9fd',
  surface2: '#dfe7f3',
  border: '#cdd9ea',
  text: '#1c2638',
  muted: '#52627c',
  accent: '#2563c9',
  accentFg: '#ffffff',
  accentSoft: '#dae6fb',
  ok: '#3f8a3a',
  warn: '#b7701b',
  info: '#1d7fb8',
  danger: '#c2353b',
  purple: '#6f45bf',
};

const dark: Colors = {
  bg: '#171e2d',
  surface: '#1f2839',
  surface2: '#29344a',
  border: '#334058',
  text: '#eef2f8',
  muted: '#a0aec6',
  accent: '#5cc8f0',
  accentFg: '#06192a',
  accentSoft: '#163a57',
  ok: '#9fdc8a',
  warn: '#f4cf72',
  info: '#7aa7f5',
  danger: '#f08a8f',
  purple: '#b79cf5',
};

function theme(scheme: Scheme, colors: Colors): Theme {
  return {
    scheme,
    colors,
    font: {
      size: { xxs: 10.5, xs: 11, sm: 12, base: 13, md: 14, lg: 18, input: 16 },
      weight: { regular: '400', medium: '500', semibold: '600', bold: '700' },
    },
    touch: { bar: 52, control: 44, row: 44 },
    space: { 1: 4, 2: 8, 3: 12, 4: 16, 5: 24 },
    radius: { xs: 3, sm: 6, md: 8, lg: 10 },
    stateCategory: {
      proposed: colors.muted,
      inProgress: colors.info,
      resolved: colors.warn,
      completed: colors.ok,
      removed: colors.danger,
    },
  };
}

export const themes: Record<Scheme, Theme> = {
  light: theme('light', light),
  dark: theme('dark', dark),
};
