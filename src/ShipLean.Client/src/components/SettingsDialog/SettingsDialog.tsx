import { useState } from 'react';
import { Dialog } from '../Dialog';
import { showToast } from '../toastStore';
import { settingsDialogTestIds as ids } from '../../features/settingsDialog/testIds';
import { SettingsFooter } from './SettingsFooter';
import { SettingsNav } from './SettingsNav';
import { sameValue } from './sameValue';
import { isDisabledPane, type ActivePaneDef, type PaneGroup, type SaveOutcome } from './types';
import { useSettingsDraft } from './useSettingsDraft';

/**
 * The frame of the board and Taskboard settings: a grouped nav of panes, a dot on each pane with unsaved changes, a
 * footer naming them, and Save / Cancel. `onSave` holds changes until Save and asks Save or Cancel before closing with
 * any pending; `immediately` sends each change as it is made.
 */
export function SettingsDialog<TDoc extends object, TContext>({
  title,
  width,
  groups,
  document,
  context,
  apply,
  section,
  defaultSection,
  savedMessage = 'Settings saved',
  onSectionChange,
  onSave,
  onClose,
}: {
  title: string;
  width?: string;
  groups: PaneGroup<TDoc, TContext>[];
  document: TDoc;
  context: TContext;
  apply: 'onSave' | 'immediately';
  /** The pane to show; an id that is not a selectable pane opens `defaultSection`. */
  section: string | undefined;
  defaultSection: string;
  /** The toast after a Save (onSave mode) that closes the dialog. */
  savedMessage?: string;
  onSectionChange(id: string): void;
  /** Sends `draft`; `base` is the document the draft was made from, for the server's merge. */
  onSave(draft: TDoc, base: TDoc): Promise<SaveOutcome<TDoc>>;
  onClose(): void;
}) {
  const panes = groups.flatMap((group) => group.panes);
  const labelOfSection = (name: string) =>
    panes.find((pane) => pane.sections.some((key) => String(key) === name))?.label ?? name;
  const { draft, base, change, save, resend, saving, error } = useSettingsDraft({
    document,
    apply,
    onSave,
    onMerged: (sections) =>
      showToast(`Someone else changed ${[...new Set(sections.map(labelOfSection))].join(', ')}`, {
        tone: 'warn',
      }),
    onFailed: (message) => showToast(`Could not save: ${message}`, { tone: 'danger' }),
  });
  const [askingToClose, setAskingToClose] = useState(false);

  const selectable = (id: string | undefined) =>
    panes.find(
      (pane): pane is ActivePaneDef<TDoc, TContext> => !isDisabledPane(pane) && pane.id === id,
    );
  const active =
    selectable(section) ??
    selectable(defaultSection) ??
    panes.find((pane): pane is ActivePaneDef<TDoc, TContext> => !isDisabledPane(pane));

  // Immediate mode applies each change at once, so a pane is only unsaved there after a failed save.
  const dirtyPanes =
    apply === 'onSave' || error !== undefined
      ? panes.filter(
          (pane) =>
            !isDisabledPane(pane) && pane.sections.some((key) => !sameValue(draft[key], base[key])),
        )
      : [];
  const warnings = new Map<string, string>();
  for (const pane of panes) {
    const message = pane.warning?.(draft, context) ?? null;
    if (message !== null) warnings.set(pane.id, message);
  }
  const warned = panes.filter((pane) => warnings.has(pane.id));

  const requestClose = () => {
    if (dirtyPanes.length > 0) setAskingToClose(true);
    else onClose();
  };
  const cancel = () => {
    if (dirtyPanes.length > 0) showToast('Changes discarded. Back to the last saved settings');
    onClose();
  };
  const saveAndClose = async () => {
    if (await save()) {
      showToast(savedMessage, { tone: 'ok' });
      onClose();
    } else setAskingToClose(false);
  };
  const activeWarning = active === undefined ? undefined : warnings.get(active.id);

  return (
    <Dialog
      title={title}
      width={width}
      onClose={requestClose}
      footer={
        apply === 'onSave' || error !== undefined || warned.length > 0 ? (
          <SettingsFooter
            apply={apply}
            dirtyLabels={dirtyPanes.map((pane) => pane.label)}
            warnings={warned.map((pane) => ({ id: pane.id, message: warnings.get(pane.id) ?? '' }))}
            error={error}
            saving={saving}
            askingToClose={askingToClose && dirtyPanes.length > 0}
            onSave={() => void saveAndClose()}
            onRetry={apply === 'onSave' ? () => void saveAndClose() : resend}
            onCancel={cancel}
            onSaveAndClose={() => void saveAndClose()}
            onSelectPane={onSectionChange}
          />
        ) : undefined
      }
    >
      <div className="grid min-h-[420px] grid-cols-[200px_minmax(0,1fr)] gap-4">
        <SettingsNav
          groups={groups}
          activeId={active?.id}
          dirtyIds={new Set(dirtyPanes.map((pane) => pane.id))}
          warnings={warnings}
          onSelect={onSectionChange}
        />
        <div data-testid={ids.paneBody} className="grid content-start gap-3">
          {apply === 'immediately' ? (
            <p
              data-testid={ids.immediateNote}
              className="m-0 text-[length:var(--text-xs)] text-ink-muted"
            >
              Changes apply straight away.
            </p>
          ) : null}
          {activeWarning === undefined ? null : (
            <div
              data-testid={ids.paneWarning}
              role="status"
              className="rounded-panel border border-edge border-l-[3px] border-l-caution bg-panel px-3 py-2 text-[length:var(--text-sm)]"
            >
              {activeWarning}
            </div>
          )}
          {active === undefined ? null : (
            <active.component
              draft={draft}
              onChange={change}
              readOnly={saving && apply === 'onSave'}
              context={context}
            />
          )}
        </div>
      </div>
    </Dialog>
  );
}
