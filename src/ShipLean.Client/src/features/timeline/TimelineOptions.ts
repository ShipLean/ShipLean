export const TIMELINE_GROUPS = ['stories', 'people', 'tasks'] as const;
export const TIMELINE_ZOOMS = ['fit', 'days'] as const;
export const TIMELINE_VIEWS = ['chart', 'json'] as const;

export interface TimelineOptions {
  group: (typeof TIMELINE_GROUPS)[number];
  zoom: (typeof TIMELINE_ZOOMS)[number];
  deps: boolean;
  cp: boolean;
  view: (typeof TIMELINE_VIEWS)[number];
}

export const DEFAULT_TIMELINE_OPTIONS: TimelineOptions = {
  group: 'stories',
  zoom: 'fit',
  deps: true,
  cp: true,
  view: 'chart',
};

const oneOf = <T extends string>(values: readonly T[], value: string | null, fallback: T): T =>
  values.find((v) => v === value) ?? fallback;

/** The options an address carries; a missing or unknown value is the default. Only `off` turns deps and cp off. */
export function optionsFromQuery(search: string): TimelineOptions {
  const params = new URLSearchParams(search);
  const defaults = DEFAULT_TIMELINE_OPTIONS;
  return {
    group: oneOf(TIMELINE_GROUPS, params.get('group'), defaults.group),
    zoom: oneOf(TIMELINE_ZOOMS, params.get('zoom'), defaults.zoom),
    deps: params.get('deps') !== 'off',
    cp: params.get('cp') !== 'off',
    view: params.get('view') === 'json' ? 'json' : defaults.view,
  };
}

/** The query pairs for the options that differ from the defaults, so the default address stays short. */
export function optionsToQuery(options: TimelineOptions): URLSearchParams {
  const defaults = DEFAULT_TIMELINE_OPTIONS;
  const params = new URLSearchParams();
  if (options.group !== defaults.group) params.set('group', options.group);
  if (options.zoom !== defaults.zoom) params.set('zoom', options.zoom);
  if (!options.deps) params.set('deps', 'off');
  if (!options.cp) params.set('cp', 'off');
  if (options.view !== defaults.view) params.set('view', options.view);
  return params;
}

/** The selected item key, or null when the address names none. */
export function selectedFromQuery(search: string): string | null {
  const value = new URLSearchParams(search).get('select');
  return value === null || value === '' ? null : value;
}
