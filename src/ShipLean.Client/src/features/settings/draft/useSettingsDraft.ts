import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { ApiRequestError } from '../../../api/client';
import type { ProjectSettingsDto } from '../../../api/projects';
import { projectsKey } from '../../../queries/shared';
import { projectSettingsKey, saveProjectSettings } from '../settingsApi';
import {
  changesBetween,
  parse,
  rebase,
  sameSettings,
  toDraft,
  toPut,
  validate,
  type FieldError,
} from './model';

export type SaveResult = { ok: true } | { ok: false; errors: FieldError[] };

// The one draft of every tab: edited in place, applied by one save, put back by discard. A newer saved document (a
// teammate's save) replaces the draft while it is untouched; otherwise it becomes the base and the user's own edits
// are replayed on top (theirs are kept), and `stale` says so.
export function useSettingsDraft(projectId: string, settings: ProjectSettingsDto) {
  const queryClient = useQueryClient();
  const [base, setBase] = useState(() => toDraft(settings));
  const [draft, setDraft] = useState(base);
  const [seen, setSeen] = useState(settings);
  const [stale, setStale] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string>();
  const [showErrors, setShowErrors] = useState(false);

  const changes = changesBetween(base, draft, seen.states);
  if (settings !== seen) {
    setSeen(settings);
    if (changes.length === 0) {
      const next = toDraft(settings);
      setBase(next);
      setDraft(next);
    } else {
      const rebased = rebase(settings, base, draft, seen.states);
      setBase(rebased.base);
      setDraft(rebased.draft);
      if (!sameSettings(settings, seen)) setStale(true);
    }
  }

  const errors = showErrors ? validate(draft, settings.states) : [];

  const discard = () => {
    setDraft(base);
    setShowErrors(false);
    setSaveError(undefined);
    setStale(false);
  };

  // Resolves with the errors to jump to, or ok. A refusal from the server is `saveError`, shown as its own message.
  const save = async (): Promise<SaveResult> => {
    setSaveError(undefined);
    const result = parse(draft, settings.states);
    setShowErrors(true);
    if ('errors' in result) return { ok: false, errors: result.errors };
    setSaving(true);
    try {
      const saved = await saveProjectSettings(projectId, toPut(draft, result.parsed));
      const next = toDraft(saved);
      setBase(next);
      setDraft(next);
      setSeen(saved);
      setStale(false);
      setShowErrors(false);
      queryClient.setQueryData(projectSettingsKey(projectId), saved);
      // This tab drops its own event: everything read from the project, and the project list, reads itself again.
      await queryClient.invalidateQueries({ queryKey: projectsKey });
      return { ok: true };
    } catch (failure) {
      if (!(failure instanceof ApiRequestError)) throw failure;
      setSaveError(failure.message);
      return { ok: false, errors: [] };
    } finally {
      setSaving(false);
    }
  };

  return { draft, setDraft, changes, errors, stale, saving, saveError, save, discard };
}
