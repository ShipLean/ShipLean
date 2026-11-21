import { createContext, useEffect, useRef, useState, type ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import type { ProjectDto, ProjectSettingsDto } from '@shiplean/core/api/projects';
import type { WorkItemTypeId } from '@shiplean/core/api/refs';
import { routePath, type ProjectSettingsTab } from '../../../routes';
import { useTopBar } from '../../../touch/TopBar';
import { useTouchShell } from '../../../touch/useTouchShell';
import { PromptsTabBadge } from '../../prompts/PromptsTabBadge';
import { settingsTestIds } from '../testIds';
import { GeneralTab } from './GeneralTab';
import { MembersTab } from './MembersTab';
import { ReadOnlyBanner } from './ReadOnlyBanner';
import { SettingsHeader } from './SettingsHeader';
import { SprintsTab } from './SprintsTab';
import { TabList } from './TabList';
import { SettingsIndex } from './touch/SettingsIndex';
import { SettingsSaveBar } from './touch/SettingsSaveBar';
import { useSettingsDraft } from './useSettingsDraft';
import type { DraftField, DraftTab } from './model';
import type { TabProps } from './tabProps';

/** What a tab's content outside this slice (the States tab) reads and writes: the one draft, never its own copy. */
export interface SettingsDraftContextValue extends TabProps {
  settings: ProjectSettingsDto;
  /** The error Save last jumped to, so a tab can bring the field into view before it is focused. */
  focus: FocusRequest | undefined;
}

export interface FocusRequest {
  tab: DraftTab;
  field: DraftField;
  type?: WorkItemTypeId;
}

export const SettingsDraftContext = createContext<SettingsDraftContextValue | undefined>(undefined);

// The frame once the settings are loaded: one draft behind every tab, one Save. States and Integrations are other
// slices' content, shown untouched on their tabs. At touch there is no header or tab row: with no tab the index lists
// the panes, and each pane is a sub-page with ‹ back to it (SHIP-1809 P1, P2). No tab is the index only at touch.
export function LoadedSettings({
  project,
  settings,
  tab,
  states,
  integrations,
  demos,
  inspection = null,
  prompts = null,
}: {
  project: ProjectDto;
  settings: ProjectSettingsDto;
  tab: ProjectSettingsTab | undefined;
  states: ReactNode;
  integrations: ReactNode;
  demos: ReactNode;
  inspection?: ReactNode;
  prompts?: ReactNode;
}) {
  const navigate = useNavigate();
  const { touch } = useTouchShell();
  useTopBar(
    touch && tab !== undefined
      ? { back: { to: routePath('projectSettingsIndex', { key: project.key }), label: 'Settings' } }
      : {},
  );
  const readOnly = project.role !== 'admin';
  const { draft, setDraft, changes, errors, stale, saving, saveError, save, discard } =
    useSettingsDraft(project.id, settings);
  const [focusRequest, setFocusRequest] = useState<FocusRequest>();
  const focused = useRef(focusRequest);

  // After Save jumped to another tab, focus the field once its tab is on screen.
  useEffect(() => {
    if (focusRequest === undefined || focusRequest === focused.current || focusRequest.tab !== tab)
      return;
    focused.current = focusRequest;
    document.getElementById(`settings-field-${focusRequest.field}`)?.focus();
  }, [focusRequest, tab]);

  const jumpTo = (target: DraftTab) => {
    if (target !== tab)
      void navigate(routePath('projectSettings', { key: project.key, tab: target }));
  };

  const submit = async () => {
    const result = await save();
    const first = result.ok ? undefined : result.errors[0];
    if (first === undefined) return;
    jumpTo(first.tab);
    setFocusRequest({ tab: first.tab, field: first.field, type: first.type });
  };

  useEffect(() => {
    if (readOnly) return;
    const onKey = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key === 's') {
        event.preventDefault();
        if (changes.length > 0 && !saving) void submit();
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  });

  const dirty = new Set<DraftTab>(changes.map((c) => c.tab));
  const admins = draft.members.filter((m) => m.role === 'admin').map((m) => m.user.name);
  const changedMembers = new Set(
    changes.flatMap((c) => (c.memberId === undefined ? [] : [c.memberId])),
  );
  const errored = new Set<DraftTab>(errors.map((e) => e.tab));
  const tabProps = { draft, setDraft, readOnly, errors };

  return (
    <div className="flex h-full min-h-0 flex-col touch:h-auto">
      {touch ? null : (
        <SettingsHeader
          projectName={project.name}
          changes={changes}
          onJump={(change) => jumpTo(change.tab)}
          readOnly={readOnly}
          saving={saving}
          errorCount={errors.length}
          onSave={() => void submit()}
          onDiscard={discard}
        />
      )}
      <div className="grid min-h-0 flex-1 grid-cols-[200px_minmax(0,1fr)] touch:grid-cols-[minmax(0,1fr)]">
        {touch || tab === undefined ? null : (
          <TabList
            projectKey={project.key}
            active={tab}
            dirty={dirty}
            errored={errored}
            badges={{ prompts: <PromptsTabBadge projectId={project.id} /> }}
          />
        )}
        <main className="min-h-0 overflow-auto p-4 touch:overflow-visible touch:p-0">
          {readOnly ? <ReadOnlyBanner projectName={project.name} admins={admins} /> : null}
          {saveError === undefined ? null : (
            <div
              role="alert"
              data-testid={settingsTestIds.saveError}
              className="mb-4 rounded-panel border border-alert px-4 py-2 text-[length:var(--text-sm)] text-alert"
            >
              {saveError}
            </div>
          )}
          {stale ? (
            <div
              data-testid={settingsTestIds.staleNotice}
              className="mb-4 rounded-panel border border-edge px-4 py-2 text-[length:var(--text-sm)] text-ink"
            >
              A teammate saved these settings; your edits were kept on top of theirs.
            </div>
          ) : null}
          {tab === undefined && (
            <SettingsIndex
              projectKey={project.key}
              projectName={project.name}
              memberCount={draft.members.length}
              dirty={dirty}
              errored={errored}
            />
          )}
          {tab === 'general' && (
            <GeneralTab
              project={project}
              agentConnections={settings.general.agentConnections}
              {...tabProps}
            />
          )}
          {tab === 'members' && <MembersTab {...tabProps} changedMembers={changedMembers} />}
          {tab === 'sprints' && <SprintsTab project={project} {...tabProps} />}
          <SettingsDraftContext.Provider value={{ settings, focus: focusRequest, ...tabProps }}>
            {tab === 'states' && states}
            {tab === 'integrations' && integrations}
            {tab === 'demos' && demos}
            {tab === 'inspection' && inspection}
            {tab === 'prompts' && prompts}
          </SettingsDraftContext.Provider>
          {/* Demos, Inspection and Prompts have their own Save; the tab dots still show the draft is dirty. */}
          {readOnly || tab === 'demos' || tab === 'inspection' || tab === 'prompts' ? null : (
            <SettingsSaveBar
              changes={changes}
              saving={saving}
              onJump={(change) => jumpTo(change.tab)}
              onSave={() => void submit()}
              onDiscard={discard}
            />
          )}
        </main>
      </div>
    </div>
  );
}
