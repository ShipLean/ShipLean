import { useEffect, useRef } from 'react';
import { Link } from 'react-router-dom';
import { routePath, type ProjectSettingsTab } from '../../../routes';
import { settingsTestIds } from '../testIds';

const LABELS: Record<ProjectSettingsTab, string> = {
  general: 'General',
  states: 'States',
  members: 'Members',
  sprints: 'Sprints & forecast',
  integrations: 'Integrations',
  demos: 'Demos',
};
const TEST_IDS: Record<ProjectSettingsTab, string> = {
  general: settingsTestIds.tabGeneral,
  states: settingsTestIds.tabStates,
  members: settingsTestIds.tabMembers,
  sprints: settingsTestIds.tabSprints,
  integrations: settingsTestIds.tabIntegrations,
  demos: settingsTestIds.tabDemos,
};
const ORDER: ProjectSettingsTab[] = [
  'general',
  'states',
  'members',
  'sprints',
  'integrations',
  'demos',
];

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
  const nav = useRef<HTMLElement>(null);
  // At touch the tabs are one row that scrolls in its own box: bring the active tab to the middle of it.
  useEffect(() => {
    const center = () => {
      const row = nav.current;
      const current = row?.querySelector<HTMLElement>('[aria-current="page"]');
      if (row && current)
        row.scrollLeft = current.offsetLeft - (row.clientWidth - current.offsetWidth) / 2;
    };
    center();
    // Web fonts change the tabs' widths once they load.
    let live = true;
    void document.fonts.ready.then(() => {
      if (live) center();
    });
    return () => {
      live = false;
    };
  }, [active]);
  return (
    <nav
      ref={nav}
      aria-label="Settings sections"
      data-testid={settingsTestIds.tabRow}
      className="flex flex-col gap-0.5 border-r border-edge bg-panel p-2 touch:mb-3 touch:flex-row touch:gap-1 touch:overflow-x-auto touch:rounded-panel touch:border touch:border-edge touch:p-1 touch:[scrollbar-width:none]"
    >
      {ORDER.map((tab) => (
        <Link
          key={tab}
          to={routePath('projectSettings', { key: projectKey, tab })}
          data-testid={TEST_IDS[tab]}
          aria-current={tab === active ? 'page' : undefined}
          className={`flex h-[30px] items-center gap-2 rounded-control px-2 text-[length:var(--text-sm)] touch:h-[var(--touch-target)] touch:flex-none touch:whitespace-nowrap touch:px-3 ${
            tab === active ? 'bg-signal-soft font-semibold text-ink' : 'text-ink-muted'
          }`}
        >
          {LABELS[tab]}
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
