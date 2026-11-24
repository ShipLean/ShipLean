import { useState } from 'react';
import type { PromptDto, PromptStatus, PromptsPageDto } from '@shiplean/core/api/prompts';
import { promptCustomizeOpened } from '@shiplean/core/api/usageApi';
import { Chip, type ChipTone } from '../../../components/Chip';
import { usePromptHistory, useUseVersion } from '../usePrompts';
import { PromptEditor } from '../editor/PromptEditor';
import type { EditorState } from '../PromptsPage';
import { promptsTestIds as ids } from '../testIds';
import { AssembledTab } from './AssembledTab';
import { Callout } from './Callout';
import { promptDiffHunks } from './diffParse';
import { HistoryTab } from './HistoryTab';
import { PromptActions } from './PromptActions';
import { PromptFacts } from './PromptFacts';
import { RenderedTab } from './RenderedTab';
import { SourceTab } from './SourceTab';
import { TabBand, type TabBandTab } from './TabBand';
import { UpdateBanner } from './UpdateBanner';
import { UpdateTab } from './UpdateTab';
import { UseBuiltInDialog } from './UseBuiltInDialog';
import { YourChangesTab } from './YourChangesTab';

type TabId = 'source' | 'rendered' | 'yourChanges' | 'update' | 'history' | 'assembled';

// The dot is the strip's custom-dot style: purple for a custom in use, amber for a built-in that changed under it.
const STATUS: Record<PromptStatus, { tone: ChipTone; testId: string; dot: string | null }> = {
  builtIn: { tone: 'muted', testId: ids.chipBuiltIn, dot: null },
  custom: { tone: 'purple', testId: ids.chipCustom, dot: 'bg-label-purple' },
  builtInUpdated: { tone: 'warn', testId: ids.chipBuiltInUpdated, dot: 'bg-caution' },
  usingBuiltIn: { tone: 'muted', testId: ids.chipUsingBuiltIn, dot: null },
};

/** "+N −M" from the custom's patch against the built-in, or nothing when there is no patch. */
function changeCount(patch: string | null): string | undefined {
  if (patch === null) return undefined;
  const lines = promptDiffHunks(patch).flatMap((hunk) => hunk.lines);
  const added = lines.filter((line) => line.kind === 'add').length;
  const removed = lines.filter((line) => line.kind === 'del').length;
  return `+${added} \u2212${removed}`;
}

function statusLabel(prompt: PromptDto): string {
  const number = prompt.custom?.number;
  switch (prompt.status) {
    case 'builtIn':
      return 'Built-in';
    case 'custom':
      return `Custom v${number}`;
    case 'builtInUpdated':
      return 'Built-in updated';
    case 'usingBuiltIn':
      return 'Using built-in';
  }
}

// One prompt: its name, file, status and actions, the update banner, and the tabs (or the editor, once it is open).
export function PromptDetail({
  projectId,
  projectName,
  page,
  prompt,
  editor,
  openEditor,
  closeEditor,
  onEditorDirtyChange,
}: {
  projectId: string;
  projectName: string;
  page: PromptsPageDto;
  prompt: PromptDto;
  editor: EditorState | null;
  openEditor: (state: EditorState) => void;
  closeEditor: () => void;
  onEditorDirtyChange: (dirty: boolean) => void;
}) {
  const [tab, setTab] = useState<TabId>('source');
  const [confirmingUseBuiltIn, setConfirmingUseBuiltIn] = useState(false);
  const custom = prompt.custom;
  const [resumeError, setResumeError] = useState<string | null>(null);
  const resume = useUseVersion(projectId, prompt.key);
  const inUseText = custom?.inUse === true ? custom.text : prompt.builtInText;

  // A paused prompt's custom version is the newest one: Edit custom version starts from its text, not the built-in's.
  const editText = prompt.status === 'usingBuiltIn' && custom !== null ? custom.text : inUseText;
  const open = (state: EditorState) => {
    if (editor !== null) return;
    void promptCustomizeOpened(projectId, prompt.key).catch(() => undefined);
    openEditor(state);
  };
  const applyAndEdit = () => {
    if (prompt.update !== null)
      open({ initialText: prompt.update.mergedText, source: 'builtInUpdate' });
  };

  // The same query HistoryTab reads (Contract 13): the count comes from its cache, and opening History shows the
  // cached versions at once while the query refetches them in the background.
  const history = usePromptHistory(projectId, prompt.key, custom !== null);
  const historyCount = custom === null ? undefined : history.data?.versions.length;

  const tabs: (TabBandTab<TabId> & { show: boolean })[] = [
    {
      id: 'source',
      label: custom?.inUse === true ? `Custom v${custom.number}` : 'Built-in prompt',
      testId: ids.tabSource,
      show: true,
    },
    { id: 'rendered', label: 'Rendered', testId: ids.tabRendered, show: true },
    {
      id: 'yourChanges',
      label: 'Your changes',
      count: changeCount(prompt.yourChangesPatch),
      testId: ids.tabYourChanges,
      show: custom !== null,
    },
    { id: 'update', label: 'Built-in update', testId: ids.tabUpdate, show: prompt.update !== null },
    { id: 'history', label: 'History', count: historyCount, testId: ids.tabHistory, show: true },
    { id: 'assembled', label: 'What the agent gets', testId: ids.tabAssembled, show: true },
  ];
  const readOnlyNote = page.canEdit ? null : (
    <Callout tone="info" testId={ids.readOnlyNote}>
      <span>
        You can read every prompt. Only project admins can customize them: ask{' '}
        <span data-testid={ids.readOnlyAdmins}>{page.adminNames.join(' or ')}</span>.
      </span>
    </Callout>
  );
  const active = tabs.find((t) => t.id === tab && t.show)?.id ?? 'source';
  const status = STATUS[prompt.status];

  return (
    <section
      data-testid={ids.detail}
      aria-label={prompt.name}
      className="grid min-w-0 content-start rounded-panel border border-edge bg-panel"
    >
      <header className="grid gap-2 border-b border-edge p-3">
        <div className="flex flex-wrap items-center gap-2">
          <h2
            data-testid={ids.detailName}
            className="m-0 text-[length:var(--text-lg)] font-semibold"
          >
            {prompt.name}
          </h2>
          <span data-testid={ids.detailChip} data-status={prompt.status}>
            <span data-testid={status.testId}>
              <Chip tone={status.tone}>
                {status.dot === null ? null : (
                  <span
                    aria-hidden="true"
                    data-testid={ids.detailChipDot}
                    className={`size-1.5 rounded-full ${status.dot}`}
                  />
                )}
                {statusLabel(prompt)}
              </Chip>
            </span>
          </span>
          <span
            data-testid={ids.detailFile}
            className="font-[family-name:var(--font-mono)] text-[length:var(--text-xs)] text-ink-muted"
          >
            {prompt.file}
          </span>
          {editor === null ? (
            <PromptActions
              prompt={prompt}
              canEdit={page.canEdit}
              onEdit={() => open({ initialText: editText, source: 'editor' })}
              onUseBuiltIn={() => setConfirmingUseBuiltIn(true)}
              onResume={(number) => {
                setResumeError(null);
                resume.mutateAsync(number).catch((error: unknown) => {
                  setResumeError(
                    error instanceof Error
                      ? error.message
                      : 'The version could not be put back in use.',
                  );
                });
              }}
              onCopy={() => void navigator.clipboard.writeText(inUseText).catch(() => undefined)}
            />
          ) : null}
        </div>
        {editor === null ? (
          <>
            {resumeError === null ? null : (
              <div role="alert" data-testid={ids.actionError} className="text-alert">
                {resumeError}
              </div>
            )}
            <PromptFacts prompt={prompt} />
            {prompt.status === 'custom' && custom !== null ? (
              <Callout tone="purple" testId={ids.inUseCallout}>
                <span>
                  Agents in this project get{' '}
                  <b className="font-semibold">custom v{custom.number}</b> ({custom.by},{' '}
                  {custom.atLabel}) instead of the built-in.
                </span>
              </Callout>
            ) : null}
            {prompt.status === 'usingBuiltIn' && custom !== null ? (
              <Callout tone="info" testId={ids.pausedCallout}>
                <span>
                  Agents get the <b className="font-semibold">built-in</b> prompt. Your custom v
                  {custom.number} is kept in History.
                </span>
              </Callout>
            ) : null}
            <UpdateBanner
              projectId={projectId}
              prompt={prompt}
              canEdit={page.canEdit}
              onApplyAndEdit={applyAndEdit}
              onReview={() => setTab('update')}
            />
          </>
        ) : null}
        {readOnlyNote}
      </header>
      {editor === null ? (
        <>
          <div data-testid={ids.tabBar}>
            <TabBand
              label={`Views of ${prompt.name}`}
              tabs={tabs.filter((t) => t.show)}
              active={active}
              onChange={setTab}
            />
          </div>
          <div className="min-w-0 p-3">
            {active === 'source' && <SourceTab prompt={prompt} />}
            {active === 'rendered' && <RenderedTab projectId={projectId} text={inUseText} />}
            {active === 'yourChanges' && <YourChangesTab prompt={prompt} />}
            {active === 'update' && (
              <UpdateTab
                projectId={projectId}
                prompt={prompt}
                canEdit={page.canEdit}
                onApplyAndEdit={applyAndEdit}
              />
            )}
            {active === 'history' && (
              <HistoryTab
                projectId={projectId}
                prompt={prompt}
                canEdit={page.canEdit}
                onUseBuiltIn={() => setConfirmingUseBuiltIn(true)}
              />
            )}
            {active === 'assembled' && (
              <AssembledTab projectId={projectId} prompt={prompt} agents={page.agents} />
            )}
          </div>
        </>
      ) : (
        <PromptEditor
          projectId={projectId}
          prompt={prompt}
          initialText={editor.initialText}
          source={editor.source}
          onClose={closeEditor}
          onDirtyChange={onEditorDirtyChange}
        />
      )}
      <UseBuiltInDialog
        projectId={projectId}
        projectName={projectName}
        prompt={prompt}
        open={confirmingUseBuiltIn}
        onClose={() => setConfirmingUseBuiltIn(false)}
      />
    </section>
  );
}
