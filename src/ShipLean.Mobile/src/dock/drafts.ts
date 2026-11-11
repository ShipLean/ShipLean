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
// The item ids that hold a draft in a scope. The store cannot list its keys, so sign-out clears through this.
const INDEX_PREFIX = 'shiplean.dockDraftIndex';

function indexKey(scope: string): string {
  return `${INDEX_PREFIX}\n${scope}`;
}

function readIndex(store: KeyValueStore, scope: string): string[] {
  const raw = store.getItem(indexKey(scope));
  if (raw === null) return [];
  try {
    const parsed: unknown = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed.filter((v): v is string => typeof v === 'string') : [];
  } catch {
    return [];
  }
}

function writeIndex(store: KeyValueStore, scope: string, ids: string[]): void {
  if (ids.length === 0) store.removeItem(indexKey(scope));
  else store.setItem(indexKey(scope), JSON.stringify(ids));
}

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
  const ids = readIndex(store, scope);
  if (!ids.includes(itemId)) writeIndex(store, scope, [...ids, itemId]);
}

export function clearDraft(store: KeyValueStore, scope: string, itemId: string): void {
  store.removeItem(draftKey(scope, itemId));
  const ids = readIndex(store, scope);
  if (ids.includes(itemId)) {
    writeIndex(
      store,
      scope,
      ids.filter((id) => id !== itemId),
    );
  }
}

// An explicit sign-out: every draft of that scope goes. A lost session keeps them (nothing is lost).
export function clearAllDrafts(store: KeyValueStore, scope: string): void {
  for (const id of readIndex(store, scope)) store.removeItem(draftKey(scope, id));
  store.removeItem(indexKey(scope));
}
