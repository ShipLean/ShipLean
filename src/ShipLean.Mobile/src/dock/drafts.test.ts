import { memoryStore } from '@shiplean/core/platform';
import { describe, expect, it } from 'vitest';
import { scopeOf } from '../actions/scope';
import { clearAllDrafts, clearDraft, draftKey, loadDraft, saveDraft } from './drafts';

const joel = scopeOf({ origin: 'https://shiplean.test', email: 'joel@example.com' })!;

describe('drafts', () => {
  it('keeps a draft per item with its mode', () => {
    const store = memoryStore();
    saveDraft(store, joel, 'item-1', { text: 'Sam says the fix lands tomorrow', mode: 'note' });
    saveDraft(store, joel, 'item-2', { text: 'approve D10', mode: 'command' });

    expect(loadDraft(store, joel, 'item-1')).toEqual({
      text: 'Sam says the fix lands tomorrow',
      mode: 'note',
    });
    expect(loadDraft(store, joel, 'item-2')).toEqual({ text: 'approve D10', mode: 'command' });
    expect(loadDraft(store, joel, 'item-3')).toBeNull();
  });

  it('clears on send and when emptied', () => {
    const store = memoryStore();
    saveDraft(store, joel, 'item-1', { text: 'a note', mode: 'note' });
    clearDraft(store, joel, 'item-1');
    expect(store.getItem(draftKey(joel, 'item-1'))).toBeNull();

    saveDraft(store, joel, 'item-1', { text: 'a note', mode: 'note' });
    saveDraft(store, joel, 'item-1', { text: '  ', mode: 'note' });
    expect(store.getItem(draftKey(joel, 'item-1'))).toBeNull();
    expect(loadDraft(store, joel, 'item-1')).toBeNull();
  });

  it('keys by origin and email so another account never sees it', () => {
    const store = memoryStore();
    saveDraft(store, joel, 'item-1', { text: 'only mine', mode: 'note' });

    const otherEmail = scopeOf({ origin: 'https://shiplean.test', email: 'tom@example.com' })!;
    const otherServer = scopeOf({ origin: 'https://other.test', email: 'joel@example.com' })!;
    expect(loadDraft(store, otherEmail, 'item-1')).toBeNull();
    expect(loadDraft(store, otherServer, 'item-1')).toBeNull();
    expect(loadDraft(store, joel, 'item-1')).toEqual({ text: 'only mine', mode: 'note' });
  });

  it('clears every draft of a scope on sign-out and leaves other scopes alone', () => {
    const store = memoryStore();
    const tom = scopeOf({ origin: 'https://shiplean.test', email: 'tom@example.com' })!;
    saveDraft(store, joel, 'item-1', { text: 'one', mode: 'note' });
    saveDraft(store, joel, 'item-2', { text: 'two', mode: 'command' });
    saveDraft(store, tom, 'item-1', { text: 'tom', mode: 'note' });
    clearDraft(store, joel, 'item-2');

    clearAllDrafts(store, joel);

    expect(loadDraft(store, joel, 'item-1')).toBeNull();
    expect(store.getItem(draftKey(joel, 'item-1'))).toBeNull();
    expect(loadDraft(store, tom, 'item-1')).toEqual({ text: 'tom', mode: 'note' });
  });
});
