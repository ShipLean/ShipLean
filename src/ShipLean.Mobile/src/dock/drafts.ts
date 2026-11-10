// Drafts (typed D9): the text left in the dock when the keyboard closes, kept on this phone per item with its mode,
// until it is sent or emptied. Keyed by the session's scope (server and email, actions/scope.ts), so another account
// on the same phone never sees it. Never synced.
import type { KeyValueStore } from '@shiplean/core/platform';
import type { ComposerMode } from './mode';

export interface Draft {
  text: string;
  mode: ComposerMode;
}

const PREFIX = 'shiplean.dockDraft';

export function draftKey(scope: string, itemId: string): string {
  return `${PREFIX}\n${scope}\n${itemId}`;
}

function isDraft(v: unknown): v is Draft {
  if (typeof v !== 'object' || v === null) return false;
  const d = v as Record<string, unknown>;
  return typeof d.text === 'string' && (d.mode === 'note' || d.mode === 'command');
}

export function loadDraft(store: KeyValueStore, scope: string, itemId: string): Draft | null {
  const raw = store.getItem(draftKey(scope, itemId));
  if (raw === null) return null;
  let parsed: unknown;
  try {
    parsed = JSON.parse(raw);
  } catch {
    return null;
  }
  return isDraft(parsed) && parsed.text.trim() !== '' ? parsed : null;
}

// Keeps the draft; blank text clears it.
export function saveDraft(store: KeyValueStore, scope: string, itemId: string, draft: Draft): void {
  if (draft.text.trim() === '') {
    clearDraft(store, scope, itemId);
    return;
  }
  store.setItem(draftKey(scope, itemId), JSON.stringify(draft));
}

export function clearDraft(store: KeyValueStore, scope: string, itemId: string): void {
  store.removeItem(draftKey(scope, itemId));
}
