import { useEffect, useEffectEvent, useRef } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { NEW_ITEM_KINDS, type NewItemKind } from '../../routes';

/**
 * A backlog address that gains ?new=<kind> opens the backlog's way of creating that kind, then drops `new` (replace,
 * every other query kept), so a reload or Back doesn't open it again. A value the page doesn't accept, or isn't a kind
 * at all, is dropped without calling `onKind`. It reads the raw search param, because the parsed route query only
 * holds real kinds. It watches the address, so it works when the page is already mounted.
 */
export function useNewItemFromLink(
  accepts: readonly NewItemKind[],
  onKind: (kind: NewItemKind) => void,
) {
  const { pathname, search, hash, key } = useLocation();
  const navigate = useNavigate();
  const raw = new URLSearchParams(search).get('new');

  // The address change lands in a transition, so the page re-renders (and StrictMode re-runs effects) while `new` is
  // still there. Each address, told apart by its key, is handled once.
  const handled = useRef<string | null>(null);
  const openKind = useEffectEvent(onKind);

  useEffect(() => {
    if (raw === null || handled.current === key) return;
    handled.current = key;
    const kind = NEW_ITEM_KINDS.find((k) => k === raw);
    if (kind !== undefined && accepts.includes(kind)) openKind(kind);
    const rest = new URLSearchParams(search);
    rest.delete('new');
    const text = rest.toString();
    void navigate({ pathname, search: text === '' ? '' : `?${text}`, hash }, { replace: true });
  }, [raw, key, accepts, search, pathname, hash, navigate]);
}
