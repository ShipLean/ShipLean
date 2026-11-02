/** What the item page keeps in its address: `?retro=<id>&decide=open&point=<id>`. */
export interface RetroItemAddress {
  /** The one retro whose points show; undefined shows every retro. */
  retro: string | undefined;
  /** True for `decide=open`: only the points still to decide. */
  decide: boolean;
  /** The point to highlight. */
  point: string | undefined;
}

export function parseRetroItemAddress(params: URLSearchParams): RetroItemAddress {
  return {
    retro: params.get('retro') || undefined,
    decide: params.get('decide') === 'open',
    point: params.get('point') || undefined,
  };
}

/** The address with filters changed; an unset filter drops its parameter, and a changed filter drops `point`. */
export function withRetroItemFilters(
  params: URLSearchParams,
  change: { retro?: string | undefined; decide?: boolean },
): URLSearchParams {
  const next = new URLSearchParams(params);
  if ('retro' in change) {
    if (change.retro === undefined) next.delete('retro');
    else next.set('retro', change.retro);
  }
  if (change.decide !== undefined) {
    if (change.decide) next.set('decide', 'open');
    else next.delete('decide');
  }
  return next;
}
