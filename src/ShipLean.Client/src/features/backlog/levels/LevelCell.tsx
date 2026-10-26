import { Link } from 'react-router-dom';
import type { BacklogRowDto } from '../../../api/views';
import { StateDot } from '../../../components/StateDot';
import { TypeIcon } from '../../../components/TypeIcon';
import { routePath } from '../../../routes';
import { ParentCell } from '../list/ParentCell';
import { backlogTestIds as ids } from '../testIds';
import type { LevelColumnId } from './levelColumns';
import { LevelPoints } from './LevelPoints';
import { LevelProgress } from './LevelProgress';
import { countUnder } from './countUnder';

// One cell of a Features or Epics row.
export function LevelCell({
  row,
  column,
  projectKey,
}: {
  row: BacklogRowDto;
  column: LevelColumnId;
  projectKey: string;
}) {
  switch (column) {
    case 'rank':
      return <span data-testid={ids.rank}>{row.rank}</span>;
    case 'key':
      return <span data-testid={ids.key}>{row.key}</span>;
    case 'title':
      return (
        <span className="inline-flex items-center gap-1.5">
          {row.type ? <TypeIcon type={row.type.id} /> : null}
          <Link
            data-testid={ids.title}
            to={routePath('workItem', { key: projectKey, itemKey: row.key })}
          >
            {row.title}
          </Link>
        </span>
      );
    case 'parent':
      return (
        <span className="inline-flex items-center gap-1.5">
          <ParentCell parent={row.parent} />
          {row.parent?.type.id === 'feature' ? (
            <span
              data-testid={ids.levelNested}
              className="text-[length:var(--text-xs)] text-ink-muted"
            >
              (nested)
            </span>
          ) : null}
        </span>
      );
    case 'state':
      return row.state ? (
        <span data-testid={ids.state}>
          <StateDot category={row.state.category} name={row.state.name} />
        </span>
      ) : null;
    case 'features':
      return <span data-testid={ids.levelFeatureCount}>{countUnder(row, 'feature').total}</span>;
    case 'points':
      return <LevelPoints row={row} />;
    case 'progress':
      return <LevelProgress row={row} />;
  }
}
