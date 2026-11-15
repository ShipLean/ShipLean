import { useState } from 'react';
import { Link } from 'react-router-dom';
import type { ProjectDemoSettingsDto } from '@shiplean/core/api/demos';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { showToast } from '../../../components/toastStore';
import { routePath } from '../../../routes';
import { demosTestIds as ids } from '../../demos/testIds';
import { useSaveDemoSettings } from '../../demos/demosApi';
import { Panel } from '../draft/Panel';

const maxEnvironment = 4000;

const textarea =
  'w-full max-w-2xl touch:max-w-none rounded-control border border-edge bg-panel px-2 py-1 text-[length:var(--text-sm)] text-ink disabled:opacity-60';

/** The Demos panel: Automatic and the environment text, with a Save of its own. */
export function DemoSettingsForm({
  project,
  settings,
  readOnly,
}: {
  project: ProjectDto;
  settings: ProjectDemoSettingsDto;
  readOnly: boolean;
}) {
  const [automatic, setAutomatic] = useState(settings.automatic);
  const [environment, setEnvironment] = useState(settings.environment);
  const save = useSaveDemoSettings(project.id);
  const ready = settings.agentCheck.ready;
  // A project whose check fails later keeps Automatic on: the box can still be turned off, never on.
  const locked = readOnly || save.isPending;
  const automaticLocked = locked || (!ready && !automatic);

  return (
    <Panel title="Demos" testId={ids.demoSettingsPanel}>
      <div className="grid gap-1 text-[length:var(--text-sm)]">
        <label className="inline-flex items-center gap-2 font-medium text-ink touch:min-h-[var(--touch-target)]">
          <input
            type="checkbox"
            data-testid={ids.demoSettingsAutomatic}
            checked={automatic}
            disabled={automaticLocked}
            onChange={(event) => setAutomatic(event.target.checked)}
          />
          Make the demo when a sprint closes
        </label>
        {ready ? (
          <span className="text-ink-muted">
            Off, a closed sprint shows Create demo and someone presses it. On, the agent starts as
            the sprint closes. The demo runs as a BAU item (Business As Usual: background work that
            is never on a sprint).
          </span>
        ) : (
          <span data-testid={ids.demoSettingsAutomaticBlocked} className="text-alert">
            {settings.agentCheck.message}{' '}
            <Link
              className="text-signal"
              to={routePath('projectSettings', { key: project.key, tab: 'general' })}
            >
              Open General settings
            </Link>
          </span>
        )}
      </div>
      <label className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
        How an agent starts the environment
        <textarea
          data-testid={ids.demoSettingsEnvironment}
          className={textarea}
          rows={4}
          maxLength={maxEnvironment}
          value={environment}
          disabled={locked}
          onChange={(event) => setEnvironment(event.target.value)}
        />
        <span className="font-normal text-ink-muted">
          How an agent starts a local seeded copy of this project. It&apos;s never run against
          production. At most 4,000 characters.
        </span>
      </label>
      <div>
        <button
          type="button"
          data-testid={ids.demoSettingsSave}
          className="btn-signal touch:min-h-[var(--touch-target)] touch:w-full"
          disabled={locked}
          onClick={() =>
            save.mutate(
              { automatic, environment },
              { onError: (failure) => showToast(failure.message, { tone: 'danger' }) },
            )
          }
        >
          Save
        </button>
      </div>
    </Panel>
  );
}
