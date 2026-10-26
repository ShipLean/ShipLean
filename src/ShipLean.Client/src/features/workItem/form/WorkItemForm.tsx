import { useEffect, useState, type ReactNode } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
// Capitalised bindings get their own import declarations: specs import this module's constants, and Playwright CT's
// loader declares a capitalised binding twice when one declaration mixes it with others.
import { ApiRequestError } from '../../../api/client';
import { isApiError } from '../../../api/client';
import { conflictOf, type ConflictDto, type WorkItemDto } from '../../../api/workItems';
import { showToast } from '../../../components/toastStore';
import { FeatureForm } from '../../feature/FeatureForm';
import type { FeatureRegionProps } from '../../feature/FeatureRegionProps';
import { WorkItemConflictBanner } from '../conflict/WorkItemConflictBanner';
import { workItemTestIds as ids } from '../testIds';
import { fetchWorkItem, patchWorkItem, workItemHistoryKey, workItemKey } from '../workItemApi';
import { BeforeSaveContext } from './beforeSave';
import { useBeforeSaveGate } from './beforeSave';
import { CriteriaSection } from './CriteriaSection';
import { DescriptionSection } from './DescriptionSection';
import { DraftContext } from './DraftContext';
import { cacheIfNewer } from './cacheIfNewer';
import { changedFields, changesOf, draftOf, rebase, type Draft } from './draft';
import { FieldsStrip } from './FieldsStrip';
import { FormHeader } from './FormHeader';
import { ReproSection } from './ReproSection';
import { useUnsavedGuard } from './useUnsavedGuard';

export const STALE_MESSAGE =
  'Someone else saved this item first. Your changes are still here; they were not saved.';
export const PICKED_UP_MESSAGE = 'Assigned to you';
export const TITLE_REQUIRED_MESSAGE = 'A work item needs a title.';

// The form for one loaded item: the draft, dirty tracking, and Save writing one revision. The page's other four nodes
// are laid out around it. A Feature gets its own layout (FeatureForm), which takes no nodes: its regions are its own.
// Sections can hold Save for a confirmation through useBeforeSave.
export function WorkItemForm({
  item,
  projectKey,
  links,
  tasks,
  discussion,
  side,
}: {
  item: WorkItemDto;
  projectKey: string;
  links: ReactNode;
  tasks: ReactNode;
  discussion: ReactNode;
  side: ReactNode;
}) {
  const queryClient = useQueryClient();
  // The revision the draft is based on. A newer one arriving (another tab saved) replaces it only while the form is clean.
  const [base, setBase] = useState(item);
  const [draft, setDraft] = useState<Draft>(() => draftOf(item));
  const [error, setError] = useState<FeatureRegionProps['saveError']>(null);
  const [conflict, setConflict] = useState<ConflictDto | null>(null);
  const baseDraft = draftOf(base);
  const changed = changedFields(baseDraft, draft);
  const dirty = changed.length > 0;
  if (!dirty && item.revision !== base.revision) {
    setBase(item);
    setDraft(draftOf(item));
  }
  useUnsavedGuard(dirty);
  const gate = useBeforeSaveGate();
  const fail = (message: string, field: string | null = null) => setError({ message, field });

  const edit = (patch: Partial<Draft>) => setDraft((was) => ({ ...was, ...patch }));

  // What a save sends is kept with it: edits typed while it is in flight are laid over the saved item afterwards.
  const save = useMutation({
    mutationFn: (sent: { base: WorkItemDto; baseDraft: Draft; draft: Draft }) =>
      patchWorkItem(sent.base.id, {
        expectedRevision: sent.base.revision,
        changes: changesOf(sent.baseDraft, sent.draft),
      }),
    onSuccess: async (result, sent) => {
      if (result.notices.includes('pulled')) showToast(PICKED_UP_MESSAGE, { tone: 'ok' });
      const itemKey = sent.base.key;
      const projectId = sent.base.projectId;
      let fresh: WorkItemDto;
      try {
        fresh = await queryClient.fetchQuery({
          queryKey: workItemKey(projectId, itemKey),
          queryFn: () => fetchWorkItem(projectId, itemKey),
          staleTime: 0,
        });
      } catch (failure) {
        fail(
          `Saved as revision ${result.revision}, but reloading it failed: ${(failure as Error).message}`,
        );
        return;
      }
      void queryClient.invalidateQueries({ queryKey: workItemHistoryKey(projectId, itemKey) });
      if (fresh.revision < result.revision) {
        fail(
          `Saved as revision ${result.revision}, but the reload returned revision ${fresh.revision}. Reload the page.`,
        );
        return;
      }
      setBase(fresh);
      setDraft((was) => rebase(sent.draft, was, draftOf(fresh)));
      setError(null);
    },
    onError: (failure) => {
      const stale = conflictOf(failure);
      if (stale) {
        setConflict(stale);
        setError(null);
        return;
      }
      if (failure instanceof ApiRequestError && failure.status === 409) {
        fail(STALE_MESSAGE);
        return;
      }
      const body = failure instanceof ApiRequestError ? failure.body : undefined;
      fail(failure.message, isApiError(body) ? (body.field ?? null) : null);
    },
  });

  const reloadTheirs = () => {
    if (!conflict) return;
    cacheIfNewer(queryClient, conflict.current);
    setBase(conflict.current);
    setDraft(draftOf(conflict.current));
    setConflict(null);
    showToast(`Reloaded revision ${conflict.current.revision}. Your changes were dropped.`);
  };

  const reapplyMine = () => {
    if (!conflict) return;
    cacheIfNewer(queryClient, conflict.current);
    setDraft((was) => rebase(baseDraft, was, draftOf(conflict.current)));
    setBase(conflict.current);
    setConflict(null);
    showToast(
      `Your changes are back on top of revision ${conflict.current.revision}. Save to keep them.`,
    );
  };

  const submit = () => {
    if (!dirty || save.isPending || conflict) return;
    if (draft.title.trim() === '') {
      fail(TITLE_REQUIRED_MESSAGE);
      return;
    }
    gate.request(() => save.mutate({ base, baseDraft, draft }));
  };

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's') {
        event.preventDefault();
        submit();
      }
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  });

  const isChanged = (field: (typeof changed)[number]) => changed.includes(field);
  const type = base.type.id;
  const header = (
    <FormHeader
      item={base}
      projectKey={projectKey}
      title={draft.title}
      changed={changed}
      saving={save.isPending || conflict !== null}
      onTitleChange={(title) => edit({ title })}
      onSave={submit}
      onDiscard={() => {
        setDraft(baseDraft);
        setError(null);
        setConflict(null);
        gate.cancel();
      }}
    />
  );
  const banners = (
    <>
      {conflict ? (
        <WorkItemConflictBanner
          conflict={conflict}
          baseRevision={base.revision}
          mine={changed}
          onReloadTheirs={reloadTheirs}
          onReapplyMine={reapplyMine}
        />
      ) : null}
      {error ? (
        <div
          data-testid={ids.saveError}
          role="alert"
          className="border-b border-edge bg-sunken px-4 py-2 text-[length:var(--text-sm)] text-alert"
        >
          {error.message}
        </div>
      ) : null}
      {gate.pending ? (
        <div
          data-testid={ids.saveConfirm}
          role="alertdialog"
          aria-label="Save anyway?"
          className="flex flex-wrap items-center gap-2 border-b border-edge bg-sunken px-4 py-2 text-[length:var(--text-sm)]"
        >
          <span data-testid={ids.saveConfirmMessage} className="text-caution">
            {gate.pending.join(' ')}
          </span>
          <button
            type="button"
            data-testid={ids.saveConfirmOk}
            className="ml-auto h-6 rounded-control bg-signal px-2 font-semibold text-signal-ink"
            onClick={() => gate.confirm(() => save.mutate({ base, baseDraft, draft }))}
          >
            Save anyway
          </button>
          <button
            type="button"
            data-testid={ids.saveConfirmCancel}
            className="h-6 rounded-control border border-edge bg-panel px-2"
            onClick={gate.cancel}
          >
            Keep editing
          </button>
        </div>
      ) : null}
    </>
  );
  const description = (
    <DescriptionSection
      item={base}
      value={draft.description}
      changed={isChanged('description')}
      onChange={(description) => edit({ description })}
    />
  );
  return (
    <BeforeSaveContext.Provider value={gate.registry}>
      <DraftContext.Provider
        value={{
          links: draft.links,
          changed: isChanged('links'),
          setLinks: (nextLinks) => edit({ links: nextLinks }),
        }}
      >
        <div data-testid={ids.screen}>
          {type === 'feature' ? (
            <FeatureForm
              region={{
                item,
                draft,
                dirty,
                changed,
                setField: edit,
                saveError: error,
                projectKey,
              }}
              header={header}
              banners={banners}
              description={description}
            />
          ) : (
            <>
              {header}
              <FieldsStrip item={base} draft={draft} changed={changed} onChange={edit} />
              {banners}
              <div className="grid grid-cols-[minmax(0,1fr)_20rem] gap-4 p-4">
                <div className="grid grid-cols-[minmax(0,1fr)] content-start gap-6">
                  {description}
                  {type === 'story' ? (
                    <CriteriaSection
                      value={draft.acceptanceCriteria}
                      changed={isChanged('acceptanceCriteria')}
                      onChange={(acceptanceCriteria) => edit({ acceptanceCriteria })}
                    />
                  ) : null}
                  {type === 'bug' ? (
                    <ReproSection
                      item={base}
                      repro={draft.reproSteps}
                      severity={draft.severity}
                      reproChanged={isChanged('reproSteps')}
                      severityChanged={isChanged('severity')}
                      onReproChange={(reproSteps) => edit({ reproSteps })}
                      onSeverityChange={(severity) => edit({ severity })}
                    />
                  ) : null}
                  {tasks}
                  {discussion}
                </div>
                <aside className="grid content-start gap-4">
                  {links}
                  {side}
                </aside>
              </div>
            </>
          )}
        </div>
      </DraftContext.Provider>
    </BeforeSaveContext.Provider>
  );
}
