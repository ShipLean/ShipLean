import { Link } from 'react-router-dom';
import { routePath } from '../../routes';
import { taskChangesTestIds as ids } from './testIds';

// Under 900px the Changes page does not load (design D2): one line and a way back.
export function NarrowNotice({ projectKey, itemKey }: { projectKey: string; itemKey: string }) {
  return (
    <p data-testid={ids.narrowNotice} className="p-4 text-ink-muted">
      The changes need a wider screen.{' '}
      <Link data-testid={ids.narrowBack} to={routePath('workItem', { key: projectKey, itemKey })}>
        Back to {itemKey}
      </Link>
    </p>
  );
}
