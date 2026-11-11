// The app's theme values, light and dark (K6 shape).
import { Platform } from 'react-native';
import { generatedTokens as t } from './tokens.generated';

// Values come from tokens.generated.ts (design/tokens.json via scripts/generate-theme.mjs).
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
  // Progress bar: open work in the current sprint, and in another sprint.
  progressCurrentSprint: string;
  progressOtherSprint: string;
  // The dim over the screen behind a sheet.
  scrim: string;
  // The work item type chip fills (design/tokens.json type), by type id.
  type: Record<'epic' | 'feature' | 'story' | 'bug' | 'task' | 'action' | 'bau', string>;
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
      body15: number;
      lg: number;
      input: number;
    };
    // Monospace family for keys: iOS has no family called 'monospace'.
    mono: string;
    weight: { regular: '400'; medium: '500'; semibold: '600'; bold: '700' };
  };
  // Touch sizes: the top bar's height, a control's minimum height, a row's minimum height.
  touch: { bar: number; control: number; row: number };
  space: { 1: number; 2: number; 3: number; 4: number; 5: number };
  radius: { xs: number; sm: number; md: number; lg: number };
  stateCategory: Record<StateCategory, string>;
}

function theme(scheme: Scheme): Theme {
  const colors: Colors = t.colors[scheme];
  return {
    scheme,
    colors,
    font: { ...t.font, mono: Platform.select({ ios: 'Menlo', default: 'monospace' }) },
    touch: t.touch,
    space: t.space,
    radius: t.radius,
    stateCategory: {
      proposed: colors[t.stateCategory.proposed],
      inProgress: colors[t.stateCategory.inProgress],
      resolved: colors[t.stateCategory.resolved],
      completed: colors[t.stateCategory.completed],
      removed: colors[t.stateCategory.removed],
    },
  };
}

export const themes: Record<Scheme, Theme> = {
  light: theme('light'),
  dark: theme('dark'),
};
