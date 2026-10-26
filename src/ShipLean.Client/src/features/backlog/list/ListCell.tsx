import { Link } from 'react-router-dom';
import { Avatar } from '../../../components/Avatar';
import { StateDot } from '../../../components/StateDot';
import { TypeIcon } from '../../../components/TypeIcon';
import type { BacklogRowDto } from '../../../api/views';
import { routePath } from '../../../routes';
import type { BacklogColumnId } from '../backlogApi';
import { backlogTestIds as ids } from '../testIds';
import { ParentCell } from './ParentCell';
import { SprintCell } from './SprintCell';
import { TagsCell } from './TagsCell';

// One cell of a list row.
export function ListCell({
  row,
  column,
  projectKey,
}: {
  row: BacklogRowDto;
  column: BacklogColumnId;
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
      return <ParentCell parent={row.parent} />;
    case 'state':
      return row.state ? (
        <span data-testid={ids.state}>
          <StateDot category={row.state.category} name={row.state.name} />
        </span>
      ) : null;
    case 'points':
      return row.points === null ? (
        <span data-testid={ids.points} title="Not estimated" className="font-semibold text-caution">
          ?
        </span>
      ) : (
        <span data-testid={ids.points}>{row.points}</span>
      );
    case 'sprint':
      return <SprintCell sprint={row.sprint} />;
    case 'assignee':
      return (
        <span data-testid={ids.assignee}>
          <Avatar {...(row.assignee ?? {})} />
        </span>
      );
    case 'tags':
      return <TagsCell tags={row.tags} />;
  }
}
