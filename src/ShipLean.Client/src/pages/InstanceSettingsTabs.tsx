import { Link } from 'react-router-dom';
import { pagesTestIds } from '../features/pages/testIds';
import { routePath, instanceSettingsTabs, type InstanceSettingsTab } from '../routes';

const LABELS: Record<InstanceSettingsTab, string> = {
  general: 'General',
  users: 'Users',
  projects: 'Projects',
  integrations: 'Integrations',
  agents: 'Agents',
};

// The tabs of the instance settings: each one is its own address, so this is how you get between them.
export function InstanceSettingsTabs({ active }: { active: InstanceSettingsTab }) {
  return (
    <nav aria-label="Instance settings sections" className="mb-4 flex gap-1 border-b border-edge">
      {instanceSettingsTabs.map((tab) => (
        <Link
          key={tab}
          to={routePath('instanceSettings', { tab })}
          data-testid={pagesTestIds.instanceSettingsTab}
          data-tab={tab}
          aria-current={tab === active ? 'page' : undefined}
          className={`-mb-px flex h-[34px] items-center border-b-2 px-3 text-[length:var(--text-sm)] ${
            tab === active
              ? 'border-signal font-semibold text-ink'
              : 'border-transparent text-ink-muted hover:text-ink'
          }`}
        >
          {LABELS[tab]}
        </Link>
      ))}
    </nav>
  );
}
