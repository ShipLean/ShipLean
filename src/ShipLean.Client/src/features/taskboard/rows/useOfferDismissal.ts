import { useEffect, useState, useSyncExternalStore } from 'react';

// The phone lane draws one copy of each story per page, and ✕ on the offer hides it on all of them. The dismissed story
// ids are shared by the copies mounted at the same time, so they live as long as the board's lanes do: when the last
// shared copy unmounts the set is emptied. An entry also goes when its story stops being offered (reopened), so the
// offer shows again when it comes back.
const dismissed = new Set<string>();
const listeners = new Set<() => void>();

function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
    if (listeners.size === 0) dismissed.clear();
  };
}
const notSubscribed = () => () => undefined;

function setDismissed(storyId: string, value: boolean) {
  if (dismissed.has(storyId) === value) return;
  if (value) dismissed.add(storyId);
  else dismissed.delete(storyId);
  listeners.forEach((listener) => listener());
}

/**
 * Whether the story's "All tasks done" offer was dismissed, and the ✕ handler. `shared` (the phone lane) shares it across
 * every copy of the story on the board's pages; otherwise (desktop) it is this header's own state.
 */
export function useOfferDismissal(
  storyId: string,
  offered: boolean,
  shared: boolean,
): [boolean, () => void] {
  const [own, setOwn] = useState(false);
  const everywhere = useSyncExternalStore(shared ? subscribe : notSubscribed, () =>
    dismissed.has(storyId),
  );
  useEffect(() => {
    if (shared && !offered) setDismissed(storyId, false);
  }, [shared, offered, storyId]);
  return shared ? [everywhere, () => setDismissed(storyId, true)] : [own, () => setOwn(true)];
}
