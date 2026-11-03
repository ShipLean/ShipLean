/** What the Features page keeps in its address: `?show=open|waiting&q=<text>`. */
export type RetroFeaturesShow = 'all' | 'open' | 'waiting';

export interface RetroFeaturesAddress {
  show: RetroFeaturesShow;
  q: string;
}

export function parseRetroFeaturesAddress(params: URLSearchParams): RetroFeaturesAddress {
  const show = params.get('show');
  return { show: show === 'open' || show === 'waiting' ? show : 'all', q: params.get('q') ?? '' };
}

/** The address with one value changed; an unset value drops its parameter. */
export function withRetroFeaturesValue(
  params: URLSearchParams,
  change: Partial<RetroFeaturesAddress>,
): URLSearchParams {
  const next = new URLSearchParams(params);
  if (change.show !== undefined) {
    if (change.show === 'all') next.delete('show');
    else next.set('show', change.show);
  }
  if (change.q !== undefined) {
    if (change.q === '') next.delete('q');
    else next.set('q', change.q);
  }
  return next;
}
