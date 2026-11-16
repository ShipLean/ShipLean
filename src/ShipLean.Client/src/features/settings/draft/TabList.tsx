import { Link } from 'react-router-dom';
import { projectSettingsTabLabels, routePath, type ProjectSettingsTab } from '../../../routes';
import { inspectionTestIds } from '../../inspection/testIds';
import { settingsTestIds } from '../testIds';

const TEST_IDS: Record<ProjectSettingsTab, string> = {
  general: settingsTestIds.tabGeneral,
  states: settingsTestIds.tabStates,
  members: settingsTestIds.tabMembers,
  sprints: settingsTestIds.tabSprints,
  integrations: settingsTestIds.tabIntegrations,
  demos: settingsTestIds.tabDemos,
  inspection: inspectionTestIds.settingsTabInspection,
};
const ORDER: ProjectSettingsTab[] = [
  'general',
  'states',
  'members',
  'sprints',
  'integrations',
  'demos',
  'inspection',
];

/** The settings panes as a column of tabs, at desktop only: at touch the index (SettingsIndex) lists them instead. */
export function TabList({
  projectKey,
  active,
  dirty,
  errored,
}: {
  projectKey: string;
  active: ProjectSettingsTab;
  dirty: ReadonlySet<ProjectSettingsTab>;
  errored: ReadonlySet<ProjectSettingsTab>;
}) {
  return (
    <nav
      aria-label="Settings sections"
      data-testid={settingsTestIds.tabRow}
      className="flex flex-col gap-0.5 border-r border-edge bg-panel p-2"
    >
      {ORDER.map((tab) => (
        <Link
          key={tab}
          to={routePath('projectSettings', { key: projectKey, tab })}
          data-testid={TEST_IDS[tab]}
          aria-current={tab === active ? 'page' : undefined}
          className={`flex h-[30px] items-center gap-2 rounded-control px-2 text-[length:var(--text-sm)] ${
            tab === active ? 'bg-signal-soft font-semibold text-ink' : 'text-ink-muted'
          }`}
        >
          {projectSettingsTabLabels[tab]}
          {errored.has(tab) ? (
            <span
              data-testid={settingsTestIds.tabError}
              aria-label="Has errors"
              className="size-[7px] rounded-full bg-alert"
            />
          ) : null}
          {dirty.has(tab) ? (
            <span
              data-testid={settingsTestIds.tabDirty}
              aria-label="Unsaved changes"
              className="size-[7px] rounded-full bg-caution"
            />
          ) : null}
        </Link>
      ))}
    </nav>
  );
}
