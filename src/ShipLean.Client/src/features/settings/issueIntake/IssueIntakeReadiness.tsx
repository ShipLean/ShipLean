import { Link } from 'react-router-dom';
import type { ProjectDto } from '../../../api/projects';
import { routePath } from '../../../routes';
import { settingsTestIds as ids } from '../testIds';
import type { IssueIntakeReadiness as Readiness } from './types';

interface MissingItem {
  id: string;
  text: string;
  fix?: 'general';
}

function missingItems(readiness: Readiness): MissingItem[] {
  const items: MissingItem[] = [];
  if (!readiness.hasGitHubRepository) {
    items.push({
      id: 'repository',
      text: 'The project has no github.com repository.',
      fix: 'general',
    });
  }
  if (!readiness.hasAgentConnection) {
    items.push({ id: 'agent', text: 'The project has no agent connection.', fix: 'general' });
  }
  if (!readiness.hasGitHubToken) {
    items.push({
      id: 'token',
      text: 'No GitHub token. An instance admin sets one with permission to read issues and write comments on the repository.',
    });
  }
  if (!readiness.hasWebhookSecret) {
    items.push({
      id: 'secret',
      text: 'No webhook secret. An instance admin sets one, then adds the same secret to the repository webhook.',
    });
  }
  if (!readiness.hasRunAs) {
    items.push({
      id: 'run-as',
      text: 'No run-as member. Choose one below; intake cannot be turned on without one.',
    });
  }
  return items;
}

/** The "Ready to turn on" block: each missing part, with where to fix it. */
export function IssueIntakeReadiness({
  project,
  readiness,
}: {
  project: ProjectDto;
  readiness: Readiness;
}) {
  const missing = missingItems(readiness);
  return (
    <div data-testid={ids.issueIntakeReadiness} className="grid gap-2 text-[length:var(--text-sm)]">
      <span className="font-medium text-ink">Ready to turn on</span>
      {missing.length === 0 ? (
        <span className="text-ink-muted">Everything is in place.</span>
      ) : (
        <ul className="grid gap-2">
          {missing.map((item) => (
            <ReadinessRow key={item.id} project={project} item={item} />
          ))}
        </ul>
      )}
      <span className="text-ink-muted">
        The agent relay must also allow this repository. Whoever runs the relay adds it to the
        relay&apos;s allow-list; ShipLean cannot check that.
      </span>
    </div>
  );
}

function ReadinessRow({ project, item }: { project: ProjectDto; item: MissingItem }) {
  return (
    <li data-testid={ids.issueIntakeMissing} data-item={item.id} className="text-alert">
      {item.text}{' '}
      {item.fix === undefined ? null : (
        <Link
          className="text-signal"
          to={routePath('projectSettings', { key: project.key, tab: item.fix })}
        >
          Open General settings
        </Link>
      )}
    </li>
  );
}
