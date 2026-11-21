import { useCallback, useEffect, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import type { ProjectDto } from '@shiplean/core/api/projects';
import {
  PROMPT_KEYS,
  type PromptKey,
  type PromptStage,
  type PromptVersionSource,
} from '@shiplean/core/api/prompts';
import { promptsOpened } from '@shiplean/core/api/usageApi';
import { PromptDetail } from './detail/PromptDetail';
import { PromptList } from './list/PromptList';
import { StageStrip } from './list/StageStrip';
import { promptsTestIds as ids } from './testIds';
import { usePromptsPage } from './usePrompts';

/** What the editor opens with: the text and where it came from (the version's `source`). */
export interface EditorState {
  initialText: string;
  source: PromptVersionSource;
}

const isPromptKey = (value: string | null): value is PromptKey =>
  PROMPT_KEYS.some((key) => key === value);

// Project settings › Prompts (SHIP-744): the stage strip on top, the list left and the selected prompt right (stacked
// under 900px). The selected prompt is `?prompt=<key>` so the address says what is open and the editor's leave guard sees
// a change of prompt as navigation.
export function PromptsPage({ project }: { project: ProjectDto }) {
  const query = usePromptsPage(project.id);
  const [searchParams, setSearchParams] = useSearchParams();
  const [stage, setStage] = useState<PromptStage | null>(null);
  const [open, setOpen] = useState<(EditorState & { promptKey: PromptKey }) | null>(null);
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

  const select = useCallback(
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
  // The editor belongs to the prompt it was opened on: selecting another prompt (past the leave guard) shows no editor.
  const editor = open !== null && open.promptKey === selectedKey ? open : null;
  const openEditor = useCallback(
    (state: EditorState) => {
      if (selectedKey !== undefined) setOpen({ ...state, promptKey: selectedKey });
    },
    [selectedKey],
  );
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
          className="btn w-fit"
          onClick={() => void query.refetch()}
        >
          Try again
        </button>
      </div>
    );
  }

  return (
    <div data-testid={ids.page} className="grid min-w-0 gap-3">
      <StageStrip
        stages={page.stages}
        selected={stage}
        onSelect={(next) => setStage(next === stage ? null : next)}
      />
      <div className="grid min-w-0 grid-cols-[minmax(260px,340px)_minmax(0,1fr)] gap-3 touch:grid-cols-[minmax(0,1fr)]">
        <PromptList page={page} selectedKey={selected?.key ?? null} onSelect={select} />
        {selected === undefined ? null : (
          <PromptDetail
            key={selected.key}
            projectId={project.id}
            page={page}
            prompt={selected}
            editor={editor}
            openEditor={openEditor}
            closeEditor={closeEditor}
          />
        )}
      </div>
    </div>
  );
}
