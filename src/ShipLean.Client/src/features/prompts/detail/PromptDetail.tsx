import { useState } from 'react';
import type { PromptDto, PromptStatus, PromptsPageDto } from '@shiplean/core/api/prompts';
import { promptCustomizeOpened } from '@shiplean/core/api/usageApi';
import { Chip, type ChipTone } from '../../../components/Chip';
import { PromptEditor } from '../editor/PromptEditor';
import type { EditorState } from '../PromptsPage';
import { promptsTestIds as ids } from '../testIds';
import { AssembledTab } from './AssembledTab';
import { HistoryTab } from './HistoryTab';
import { PromptActions } from './PromptActions';
import { PromptFacts } from './PromptFacts';
import { RenderedTab } from './RenderedTab';
import { SourceTab } from './SourceTab';
import { UpdateBanner } from './UpdateBanner';
import { UpdateTab } from './UpdateTab';
import { UseBuiltInDialog } from './UseBuiltInDialog';
import { YourChangesTab } from './YourChangesTab';

type TabId = 'source' | 'rendered' | 'yourChanges' | 'update' | 'history' | 'assembled';

const STATUS: Record<PromptStatus, { tone: ChipTone; testId: string }> = {
  builtIn: { tone: 'muted', testId: ids.chipBuiltIn },
  custom: { tone: 'purple', testId: ids.chipCustom },
  builtInUpdated: { tone: 'warn', testId: ids.chipBuiltInUpdated },
  usingBuiltIn: { tone: 'muted', testId: ids.chipUsingBuiltIn },
};

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
  page,
  prompt,
  editor,
  openEditor,
  closeEditor,
}: {
  projectId: string;
  page: PromptsPageDto;
  prompt: PromptDto;
  editor: EditorState | null;
  openEditor: (state: EditorState) => void;
  closeEditor: () => void;
}) {
  const [tab, setTab] = useState<TabId>('source');
  const [confirmingUseBuiltIn, setConfirmingUseBuiltIn] = useState(false);
  const custom = prompt.custom;
  const inUseText = custom?.inUse === true ? custom.text : prompt.builtInText;

  const open = (state: EditorState) => {
    void promptCustomizeOpened(projectId, prompt.key).catch(() => undefined);
    openEditor(state);
  };
  const applyAndEdit = () => {
    if (prompt.update !== null)
      open({ initialText: prompt.update.mergedText, source: 'builtInUpdate' });
  };

  const tabs: { id: TabId; label: string; testId: string; show: boolean }[] = [
    {
      id: 'source',
      label: custom?.inUse === true ? `Custom v${custom.number}` : 'Built-in prompt',
      testId: ids.tabSource,
      show: true,
    },
    { id: 'rendered', label: 'Rendered', testId: ids.tabRendered, show: true },
    { id: 'yourChanges', label: 'Your changes', testId: ids.tabYourChanges, show: custom !== null },
    { id: 'update', label: 'Built-in update', testId: ids.tabUpdate, show: prompt.update !== null },
    { id: 'history', label: 'History', testId: ids.tabHistory, show: true },
    { id: 'assembled', label: 'What the agent gets', testId: ids.tabAssembled, show: true },
  ];
  const active = tabs.find((t) => t.id === tab && t.show)?.id ?? 'source';
  const status = STATUS[prompt.status];

  return (
    <section
      data-testid={ids.detail}
      aria-label={prompt.name}
      className="grid min-w-0 content-start gap-3 rounded-panel border border-edge bg-panel p-3"
    >
      <header className="grid gap-2">
        <div className="flex flex-wrap items-center gap-2">
          <h2
            data-testid={ids.detailName}
            className="m-0 text-[length:var(--text-lg)] font-semibold"
          >
            {prompt.name}
          </h2>
          <span
            data-testid={ids.detailFile}
            className="font-mono text-[length:var(--text-xs)] text-ink-muted"
          >
            {prompt.file}
          </span>
          <span data-testid={ids.detailChip} data-status={prompt.status}>
            <span data-testid={status.testId}>
              <Chip tone={status.tone}>{statusLabel(prompt)}</Chip>
            </span>
          </span>
        </div>
        <PromptActions
          prompt={prompt}
          canEdit={page.canEdit}
          onEdit={() => open({ initialText: inUseText, source: 'editor' })}
          onUseBuiltIn={() => setConfirmingUseBuiltIn(true)}
        />
        <PromptFacts prompt={prompt} />
        <UpdateBanner
          projectId={projectId}
          prompt={prompt}
          canEdit={page.canEdit}
          onApplyAndEdit={applyAndEdit}
          onReview={() => setTab('update')}
        />
      </header>
      {editor === null ? (
        <>
          <div role="tablist" data-testid={ids.tabBar} className="flex flex-wrap gap-1">
            {tabs
              .filter((t) => t.show)
              .map((t) => (
                <button
                  key={t.id}
                  type="button"
                  role="tab"
                  aria-selected={t.id === active}
                  data-testid={t.testId}
                  className={t.id === active ? 'btn-signal' : 'btn'}
                  onClick={() => setTab(t.id)}
                >
                  {t.label}
                </button>
              ))}
          </div>
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
            <HistoryTab projectId={projectId} prompt={prompt} canEdit={page.canEdit} />
          )}
          {active === 'assembled' && (
            <AssembledTab projectId={projectId} prompt={prompt} agents={page.agents} />
          )}
        </>
      ) : (
        <PromptEditor
          projectId={projectId}
          prompt={prompt}
          initialText={editor.initialText}
          source={editor.source}
          onClose={closeEditor}
        />
      )}
      <UseBuiltInDialog
        projectId={projectId}
        prompt={prompt}
        open={confirmingUseBuiltIn}
        onClose={() => setConfirmingUseBuiltIn(false)}
      />
    </section>
  );
}
