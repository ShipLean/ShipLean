import { Link } from 'react-router-dom';
import { routePath } from '../../routes';
import { taskChangesTestIds as ids } from './testIds';

// Under 900px the Changes page does not load (design D2): one line and a way back.
export function NarrowNotice({ projectKey, itemKey }: { projectKey: string; itemKey: string }) {
  return (
    <div data-testid={ids.narrowNotice} className="grid gap-2 p-4">
      <b>Changes need a wider screen.</b>
      <span className="text-ink-muted">
        {`Open ${itemKey}'s changes on a computer. On a phone the task shows without them.`}
      </span>
      <Link data-testid={ids.narrowBack} to={routePath('workItem', { key: projectKey, itemKey })}>
        ← Back to {itemKey}
      </Link>
    </div>
  );
}
