import { sameValue } from './sameValue';

/**
 * A change made while a save was in flight, laid over the document that save returned: a section the user changed since
 * `sent` keeps their value; every other section takes the returned one (it may carry another writer's change).
 */
export function rebase<TDoc extends object>(sent: TDoc, latest: TDoc, returned: TDoc): TDoc {
  const next = { ...returned };
  for (const key of Object.keys(latest) as (keyof TDoc)[]) {
    if (!sameValue(latest[key], sent[key])) next[key] = latest[key];
  }
  return next;
}
