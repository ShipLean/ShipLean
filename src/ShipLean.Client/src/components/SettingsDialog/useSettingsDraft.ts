import { useEffect, useRef, useState } from 'react';
import { rebase } from './rebase';
import { sameValue } from './sameValue';
import type { SaveOutcome } from './types';

/** What the footer shows when a save did not go through; `retry` is false for a refusal that resending cannot fix. */
export interface SaveFailure {
  message: string;
  retry: boolean;
}

interface Model<TDoc> {
  /** The last document the server gave us (a save's answer or a refetch): what the draft is measured and sent against. */
  base: TDoc;
  draft: TDoc;
}

/**
 * The dialog's working copy of the document and the base it started from. `onSave` mode keeps the draft until `save()`.
 * `immediately` mode sends every change at once, one request at a time: a change made while one is in flight waits and
 * goes next, laid over the document the last request returned. When `document` changes under an open draft (a refetch,
 * a realtime invalidation) the draft is rebased onto it: sections the user has not touched take the new value, touched
 * ones keep theirs, and the new document becomes the base.
 */
export function useSettingsDraft<TDoc extends object>({
  document,
  apply,
  onSave,
  onMerged,
  onFailed,
}: {
  document: TDoc;
  apply: 'onSave' | 'immediately';
  onSave(draft: TDoc, base: TDoc): Promise<SaveOutcome<TDoc>>;
  onMerged(sections: string[]): void;
  onFailed(message: string): void;
}) {
  const [model, setModelState] = useState<Model<TDoc>>({ base: document, draft: document });
  const current = useRef(model);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<SaveFailure>();
  const busy = useRef(false);

  const setModel = (next: Model<TDoc>) => {
    current.current = next;
    setModelState(next);
  };

  useEffect(() => {
    const { base, draft } = current.current;
    if (sameValue(document, base)) return;
    const next = { base: document, draft: rebase(base, draft, document) };
    current.current = next;
    setModelState(next);
  }, [document]);

  const fail = (thrown: unknown) => {
    const message = thrown instanceof Error ? thrown.message : 'The settings could not be saved.';
    setError({ message, retry: true });
    onFailed(message);
  };

  const pump = async () => {
    if (busy.current) return;
    busy.current = true;
    setError(undefined);
    try {
      while (!sameValue(current.current.draft, current.current.base)) {
        const { base, draft: sending } = current.current;
        setSaving(true);
        let outcome: SaveOutcome<TDoc>;
        try {
          outcome = await onSave(sending, base);
        } catch (thrown) {
          fail(thrown);
          return;
        }
        if (!outcome.ok) {
          setError({ message: outcome.message, retry: false });
          return;
        }
        if (outcome.mergedSections.length > 0) onMerged(outcome.mergedSections);
        setModel({
          base: outcome.document,
          draft: rebase(sending, current.current.draft, outcome.document),
        });
      }
    } finally {
      busy.current = false;
      setSaving(false);
    }
  };

  const change = (next: TDoc) => {
    setModel({ base: current.current.base, draft: next });
    setError(undefined);
    if (apply === 'immediately') void pump();
  };

  /** Sends the draft (onSave mode). True when the server took it. */
  const save = async (): Promise<boolean> => {
    const { base, draft: sending } = current.current;
    setSaving(true);
    setError(undefined);
    try {
      const outcome = await onSave(sending, base);
      if (!outcome.ok) {
        setError({ message: outcome.message, retry: false });
        return false;
      }
      if (outcome.mergedSections.length > 0) onMerged(outcome.mergedSections);
      setModel({
        base: outcome.document,
        draft: rebase(sending, current.current.draft, outcome.document),
      });
      return true;
    } catch (thrown) {
      fail(thrown);
      return false;
    } finally {
      setSaving(false);
    }
  };

  return {
    draft: model.draft,
    base: model.base,
    change,
    save,
    resend: () => void pump(),
    saving,
    error,
  };
}
