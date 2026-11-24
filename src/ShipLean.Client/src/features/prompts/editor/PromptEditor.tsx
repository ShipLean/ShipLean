import { useEffect, useMemo, useState } from 'react';
import { isApiError } from '@shiplean/core/api/client';
import type { PromptDto, PromptVersionSource } from '@shiplean/core/api/prompts';
import { Dialog } from '../../../components/Dialog';
import { Markdown } from '../../../components/Markdown';
import { useItemMarkdown } from '../../feature/featureApi';
import { useUnsavedGuard } from '../../workItem/form/useUnsavedGuard';
import { promptsTestIds as ids } from '../testIds';
import { TabBand } from '../detail/TabBand';
import { useSavePromptVersion } from '../usePrompts';
import { MAX_PROMPT_BYTES, promptByteSize } from './byteSize';
import { changesFromBuiltIn, countChanges } from './changesDiff';
import { ChangesPanel } from './ChangesPanel';
import { EditorChecks } from './EditorChecks';
import { checkPrompt } from './promptChecks';
import { SideHeading } from './SideHeading';
import { SizeMeter } from './SizeMeter';

const MAX_NOTE = 200;

type EditorTab = 'edit' | 'preview' | 'changes';

// What the server refused: its message, and the field it names (`text` or `note`).
function refusal(error: unknown): { field: string | undefined; message: string } {
  const body = (error as { body?: unknown } | null)?.body;
  const field = isApiError(body) ? body.field : undefined;
  return {
    field,
    message: error instanceof Error ? error.message : 'The prompt could not be saved.',
  };
}

// The plain text editor with its side panel and pinned footer: Save as custom vN, Cancel, the leave guard (SHIP-744 D6, D10, D12).
export function PromptEditor(props: {
  projectId: string;
  prompt: PromptDto;
  initialText: string;
  source: PromptVersionSource;
  onClose: () => void;
  /** Tells the page whether there are unsaved edits, so it can ask before showing another prompt. */
  onDirtyChange?: (dirty: boolean) => void;
}) {
  const { projectId, prompt, initialText, source, onClose, onDirtyChange } = props;
  const [text, setText] = useState(initialText);
  const [note, setNote] = useState('');
  const [tab, setTab] = useState<EditorTab>('edit');
  const [leaving, setLeaving] = useState(false);
  const save = useSavePromptVersion(projectId, prompt.key);

  const dirty = text !== initialText || note !== '';
  useUnsavedGuard(dirty);
  useEffect(() => {
    onDirtyChange?.(dirty);
    return () => onDirtyChange?.(false);
  }, [dirty, onDirtyChange]);

  // What agents get now: P7 refuses only a save equal to it.
  const inUseText = prompt.custom?.inUse === true ? prompt.custom.text : prompt.builtInText;
  const bytes = promptByteSize(text);
  const checks = checkPrompt(text, prompt);
  const nextNumber = (prompt.custom?.number ?? 0) + 1;
  const canSave =
    !save.isPending &&
    note.length <= MAX_NOTE &&
    bytes <= MAX_PROMPT_BYTES &&
    text !== inUseText &&
    checks.canSave;

  // The built-in this text starts from: the new one after a built-in update, else the one the custom version was cut from.
  const baseText =
    source === 'builtInUpdate' || prompt.custom === null
      ? prompt.builtInText
      : prompt.custom.baseText;
  // The aside of the tab band: the built-in this text started from (the server's labels).
  const startedFrom =
    source === 'builtInUpdate' && prompt.update !== null
      ? `the ${prompt.update.changedLabel}`
      : prompt.custom !== null
        ? `the ${prompt.custom.baseLabel}`
        : 'the built-in of today';
  const hunks = useMemo(
    () => (tab === 'edit' ? [] : changesFromBuiltIn(baseText, text)),
    [tab, baseText, text],
  );
  const counts = useMemo(() => countChanges(changesFromBuiltIn(baseText, text)), [baseText, text]);
  const preview = useItemMarkdown(text, undefined, tab === 'preview' && text.trim() !== '');

  const doSave = () => {
    if (!canSave) return;
    save.mutate({ text, note: note.trim(), source }, { onSuccess: onClose });
  };
  // Ctrl/⌘+S saves wherever focus is in the page.
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's') {
        event.preventDefault();
        doSave();
      }
    };
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  });

  const cancel = () => (dirty ? setLeaving(true) : onClose());
  const failure = save.isError ? refusal(save.error) : null;
  const textError = failure?.field === 'text' ? failure.message : null;
  const otherError = failure !== null && failure.field !== 'text' ? failure.message : null;

  return (
    <section
      data-testid={ids.editor}
      aria-label={`Edit ${prompt.name}`}
      data-source={source}
      className="grid min-w-0"
    >
      <TabBand
        label="Editor"
        tabs={[
          { id: 'edit', label: 'Edit', testId: ids.editorTabEdit },
          { id: 'preview', label: 'Preview', testId: ids.editorTabPreview },
          {
            id: 'changes',
            label: 'Changes from built-in',
            count: `+${counts.adds} −${counts.dels}`,
            testId: ids.editorTabChanges,
          },
        ]}
        active={tab}
        onChange={setTab}
        aside={<span data-testid={ids.editorStartedFrom}>Started from {startedFrom}</span>}
      />

      <div data-testid={ids.editorBody} className="min-w-0">
        {tab === 'edit' ? (
          <div className="grid min-w-0 grid-cols-[minmax(0,1fr)_230px] touch:grid-cols-[minmax(0,1fr)]">
            <textarea
              data-testid={ids.editorText}
              aria-label={`Custom ${prompt.name} Markdown`}
              spellCheck={false}
              value={text}
              onChange={(event) => setText(event.target.value)}
              className="block h-full min-h-[460px] w-full resize-none border-0 bg-panel p-3 font-mono text-[length:var(--text-sm)] leading-[1.55] text-ink outline-none [tab-size:2] focus-visible:shadow-[inset_0_0_0_2px_var(--color-accent)]"
            />
            <div
              data-testid={ids.editorSide}
              className="grid min-w-0 content-start gap-3 border-l border-edge bg-canvas p-3 text-[length:var(--text-xs)] touch:border-t touch:border-l-0"
            >
              <EditorChecks text={text} prompt={prompt} onChange={setText} />
              <SizeMeter bytes={bytes} />
              <section>
                <SideHeading>Tips</SideHeading>
                <p data-testid={ids.editorTip} className="m-0 text-ink-muted">
                  Add project rules at the end rather than rewriting built-in sections: built-in
                  updates then apply cleanly.
                </p>
              </section>
            </div>
          </div>
        ) : null}
        {tab === 'preview' ? (
          <div data-testid={ids.editorPreview} className="min-h-[200px] p-3">
            {text.trim() === '' ? (
              <p className="m-0 text-ink-muted">Nothing to preview.</p>
            ) : preview.data ? (
              <Markdown html={preview.data.html} />
            ) : preview.isError ? (
              <p role="alert" className="m-0 text-alert">
                The preview could not be rendered.
              </p>
            ) : (
              <p className="m-0 text-ink-muted">Rendering…</p>
            )}
          </div>
        ) : null}
        {tab === 'changes' ? <ChangesPanel hunks={hunks} /> : null}
        {textError ? (
          <p
            data-testid={ids.editorError}
            role="alert"
            className="m-0 px-3 py-1 text-[length:var(--text-sm)] text-alert"
          >
            {textError}
          </p>
        ) : null}
      </div>

      {/* Pinned to the bottom of the panel, so a long side panel never pushes Save out of view (design-gaps §13). */}
      <div
        data-testid={ids.editorFooter}
        className="sticky bottom-0 z-10 grid gap-1 border-t border-edge bg-canvas px-3 py-2"
      >
        <div className="flex flex-wrap items-center gap-2">
          <input
            data-testid={ids.editorNote}
            className="field min-w-[200px] flex-1"
            aria-label="Change note"
            placeholder="What did you change? (shown in History)"
            maxLength={MAX_NOTE}
            value={note}
            onChange={(event) => setNote(event.target.value)}
          />
          <span className="text-[length:var(--text-xs)] text-ink-muted" aria-hidden>
            {note.length}/{MAX_NOTE}
          </span>
          <button
            type="button"
            data-testid={ids.editorCancel}
            className="btn touch:min-h-[var(--touch-target)]"
            onClick={cancel}
          >
            Cancel
          </button>
          <button
            type="button"
            data-testid={ids.editorSave}
            className="btn-signal touch:min-h-[var(--touch-target)]"
            disabled={!canSave}
            onClick={doSave}
          >
            {save.isPending ? 'Saving…' : `Save as custom v${nextNumber}`}
          </button>
        </div>
        {otherError ? (
          <p
            data-testid={ids.editorSaveError}
            role="alert"
            className="m-0 text-[length:var(--text-sm)] text-alert"
          >
            {otherError}
          </p>
        ) : null}
      </div>

      {leaving ? (
        <Dialog
          title="Discard your unsaved custom prompt?"
          testId={ids.editorLeaveDialog}
          width="440px"
          onClose={() => setLeaving(false)}
          footer={
            <>
              <button
                type="button"
                data-testid={ids.editorLeaveStay}
                className="btn-ghost touch:min-h-[var(--touch-target)]"
                onClick={() => setLeaving(false)}
              >
                Keep editing
              </button>
              <button
                type="button"
                data-testid={ids.editorLeaveDiscard}
                className="btn-danger-ghost touch:min-h-[var(--touch-target)]"
                onClick={onClose}
              >
                Discard
              </button>
            </>
          }
        >
          <p className="m-0">Your edits to {prompt.name} haven’t been saved.</p>
        </Dialog>
      ) : null}
    </section>
  );
}
