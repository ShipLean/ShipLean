import { Link } from 'react-router-dom';
import {
  projectSettingsTabLabels,
  projectSettingsTabs,
  routePath,
  type ProjectSettingsTab,
} from '../../../../routes';
import { settingsTestIds as ids } from '../../testIds';

// What each pane holds, as the phone design's index says it (SHIP-1809 P1).
const HOLDS: Record<ProjectSettingsTab, (projectName: string) => string> = {
  general: () => 'Name, description, working days',
  states: () => "Each type's states, their order and category",
  members: (projectName) => `Who is in ${projectName}, and their role`,
  sprints: () => 'Sprint length, velocity window and override',
  integrations: () => 'The Jira connection and import',
  demos: () => 'Automatic demos, brand and YouTube',
  inspection: () => 'Agent run inspection and agent setup',
};

/**
 * Project settings at touch with no pane picked (SHIP-1809 P1): one row per pane with what it holds, a count where the
 * draft has one, a dot for unsaved changes or errors, and › into the pane's own page.
 */
export function SettingsIndex({
  projectKey,
  projectName,
  memberCount,
  dirty,
  errored,
}: {
  projectKey: string;
  projectName: string;
  memberCount: number;
  dirty: ReadonlySet<ProjectSettingsTab>;
  errored: ReadonlySet<ProjectSettingsTab>;
}) {
  return (
    <section data-testid={ids.settingsIndex} aria-label="Settings sections" className="grid gap-2">
      <p className="m-0 text-[length:var(--text-sm)] text-ink-muted">
        {projectName} · changes apply to everyone in the project
      </p>
      <nav className="overflow-hidden rounded-panel border border-edge bg-panel">
        {projectSettingsTabs.map((tab) => (
          <Link
            key={tab}
            to={routePath('projectSettings', { key: projectKey, tab })}
            data-testid={ids.settingsIndexRow}
            className="flex min-h-[var(--touch-target)] items-center gap-3 border-b border-edge px-3 py-2.5"
          >
            <span className="grid min-w-0 flex-1 gap-0.5">
              <span className="flex items-center gap-2 text-[length:var(--text-md)] font-semibold text-ink">
                {projectSettingsTabLabels[tab]}
                {errored.has(tab) ? (
                  <span
                    data-testid={ids.tabError}
                    aria-label="Has errors"
                    className="size-[7px] rounded-full bg-alert"
                  />
                ) : null}
                {dirty.has(tab) ? (
                  <span
                    data-testid={ids.tabDirty}
                    aria-label="Unsaved changes"
                    className="size-[7px] rounded-full bg-caution"
                  />
                ) : null}
              </span>
              <span className="text-[length:var(--text-sm)] text-ink-muted">
                {HOLDS[tab](projectName)}
              </span>
            </span>
            {tab === 'members' ? (
              <span
                data-testid={ids.settingsIndexCount}
                className="text-[length:var(--text-sm)] text-ink-muted"
              >
                {memberCount}
              </span>
            ) : null}
            <span aria-hidden="true" className="text-ink-muted">
              ›
            </span>
          </Link>
        ))}
        <p className="m-0 bg-sunken px-3 py-2.5 text-[length:var(--text-sm)] text-ink-muted">
          Only project admins can change these settings. Members see them read-only.
        </p>
      </nav>
    </section>
  );
}
