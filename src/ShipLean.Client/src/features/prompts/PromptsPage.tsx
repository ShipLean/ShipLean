import { useCallback, useEffect, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { PROMPT_KEYS, type PromptKey, type PromptVersionSource } from '@shiplean/core/api/prompts';
import { promptsOpened } from '@shiplean/core/api/usageApi';
import { Dialog } from '../../components/Dialog';
import { PromptDetail } from './detail/PromptDetail';
import { HeaderSearch } from './header/HeaderSearch';
import { WaysOfWorkingHeader } from './header/WaysOfWorkingHeader';
import { StageNav } from './nav/StageNav';
import { promptsTestIds as ids } from './testIds';
import { usePromptsPage } from './usePrompts';

/** What the editor opens with: the text and where it came from (the version's `source`). */
export interface EditorState {
  initialText: string;
  source: PromptVersionSource;
}

const isPromptKey = (value: string | null): value is PromptKey =>
  PROMPT_KEYS.some((key) => key === value);

// Ways of working (SHIP-2280 Contract 7): the header with search, the stage strip with its chips and "when" line, then the
// selected prompt full width. The selected prompt is `?prompt=<key>` so the address says what is open; the stage follows
// it. A stage, chip or search pick with unsaved edits in the editor asks first.
export function PromptsPage({ project }: { project: ProjectDto }) {
  const query = usePromptsPage(project.id);
  const [searchParams, setSearchParams] = useSearchParams();
  const [open, setOpen] = useState<(EditorState & { promptKey: PromptKey }) | null>(null);
  const [editorDirty, setEditorDirty] = useState(false);
  const [switchTo, setSwitchTo] = useState<PromptKey | null>(null);
  const opened = useRef<string | undefined>(undefined);

  useEffect(() => {
    if (opened.current === project.id) return;
    opened.current = project.id;
    void promptsOpened(project.id).catch(() => undefined);
  }, [project.id]);

  const page = query.data;
  const prompts = page?.stages.flatMap((s) => s.prompts) ?? [];
  const requested = searchParams.get('prompt');
  const selected = prompts.find((p) => isPromptKey(requested) && p.key === requested) ?? prompts[0];

  const go = useCallback(
    (key: PromptKey) => {
      setSearchParams(
        (current) => {
          const next = new URLSearchParams(current);
          next.set('prompt', key);
          return next;
        },
        { replace: false },
      );
    },
    [setSearchParams],
  );
  const selectedKey = selected?.key;
  const editorOpen = open !== null && open.promptKey === selectedKey;
  // Another prompt with unsaved edits in the editor asks first; keeping the edits leaves the address as it is.
  const select = useCallback(
    (key: PromptKey) => (editorOpen && editorDirty ? setSwitchTo(key) : go(key)),
    [editorOpen, editorDirty, go],
  );
  // The editor belongs to the prompt it was opened on: selecting another prompt (past the leave guard) shows no editor.
  const editor = open !== null && open.promptKey === selectedKey ? open : null;
  const openEditor = useCallback(
    (state: EditorState) => {
      if (selectedKey !== undefined) setOpen({ ...state, promptKey: selectedKey });
    },
    [selectedKey],
  );
  // The editor belongs to the prompt it was opened on: once another prompt is selected it is gone for good.
  if (open !== null && open.promptKey !== selectedKey) setOpen(null);
  const closeEditor = useCallback(() => setOpen(null), []);

  if (query.isPending) {
    return (
      <p data-testid={ids.loading} className="p-4 text-ink-muted">
        Loading the prompts…
      </p>
    );
  }
  if (query.isError || page === undefined) {
    return (
      <div data-testid={ids.loadFailed} role="alert" className="grid gap-2 p-4 text-alert">
        <span>The prompts could not be loaded.</span>
        <button
          type="button"
          data-testid={ids.retry}
          className="btn w-fit touch:min-h-[var(--touch-target)]"
          onClick={() => void query.refetch()}
        >
          Try again
        </button>
      </div>
    );
  }

  return (
    <div data-testid={ids.page} className="grid min-w-0 gap-3">
      <WaysOfWorkingHeader
        project={project}
        customCount={page.customCount}
        unsaved={editorOpen && editorDirty}
        search={<HeaderSearch projectId={project.id} page={page} onPick={select} />}
      />
      {selected === undefined ? null : (
        <>
          <StageNav stages={page.stages} selectedKey={selected.key} onSelect={select} />
          <PromptDetail
            key={selected.key}
            projectId={project.id}
            projectName={project.name}
            page={page}
            prompt={selected}
            editor={editor}
            openEditor={openEditor}
            closeEditor={closeEditor}
            onEditorDirtyChange={setEditorDirty}
          />
        </>
      )}
      {switchTo === null ? null : (
        <Dialog
          title="Discard your unsaved custom prompt?"
          testId={ids.editorLeaveDialog}
          width="440px"
          onClose={() => setSwitchTo(null)}
          footer={
            <>
              <button
                type="button"
                data-testid={ids.editorLeaveStay}
                className="btn-ghost touch:min-h-[var(--touch-target)]"
                onClick={() => setSwitchTo(null)}
              >
                Keep editing
              </button>
              <button
                type="button"
                data-testid={ids.editorLeaveDiscard}
                className="btn-danger-ghost touch:min-h-[var(--touch-target)]"
                onClick={() => {
                  const key = switchTo;
                  setSwitchTo(null);
                  setEditorDirty(false);
                  go(key);
                }}
              >
                Discard
              </button>
            </>
          }
        >
          <p className="m-0">Your edits to {selected?.name} haven’t been saved.</p>
        </Dialog>
      )}
    </div>
  );
}
