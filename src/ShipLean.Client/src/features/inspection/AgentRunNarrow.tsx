import { Link } from 'react-router-dom';
import { routePath } from '../../routes';
import { inspectionTestIds as ids } from './testIds';

// Under 900px the run route shows one line and a link back to the item (D15).
export function AgentRunNarrow({ project, itemKey }: { project: string; itemKey: string }) {
  return (
    <p data-testid={ids.runPageNarrow}>
      Agent runs open on a wider screen.{' '}
      <Link
        data-testid={ids.runPageNarrowBack}
        className="inline-flex min-h-8 min-w-8 items-center"
        to={routePath('workItem', { key: project, itemKey })}
      >
        Back to {itemKey}
      </Link>
    </p>
  );
}
