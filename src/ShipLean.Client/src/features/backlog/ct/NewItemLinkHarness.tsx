import { Link, useLocation } from 'react-router-dom';
import type { ProjectDto } from '../../../api/projects';
import { LevelBacklog } from '../levels/LevelBacklog';
import type { LevelBacklogLevel } from '../levels/levelColumns';
import { ListMode } from '../list/ListMode';

// CT-only: a backlog with the address shown and a link that adds ?new= to it, as quick search's Create action does.
export function NewItemLinkHarness({
  project,
  level,
  kind,
}: {
  project: ProjectDto;
  level: LevelBacklogLevel | 'stories';
  kind: string;
}) {
  const { pathname, search } = useLocation();
  return (
    <>
      {level === 'stories' ? (
        <ListMode project={project} />
      ) : (
        <LevelBacklog project={project} level={level} />
      )}
      <Link data-testid="harness-link" to={{ pathname, search: `?new=${kind}` }}>
        link
      </Link>
      <span data-testid="harness-location">{pathname + search}</span>
    </>
  );
}
