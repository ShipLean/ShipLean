import { Avatar } from '../../../components/Avatar';
import { StateDot } from '../../../components/StateDot';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import { PointsCell } from '../estimate/PointsCell';
import { TitleCell } from '../selection/TitleCell';
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
      return <TitleCell row={row} projectKey={projectKey} />;
    case 'parent':
      return <ParentCell parent={row.parent} />;
    case 'state':
      return row.state ? (
        <span data-testid={ids.state}>
          <StateDot category={row.state.category} name={row.state.name} />
        </span>
      ) : null;
    case 'points':
      return <PointsCell row={row} />;
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
