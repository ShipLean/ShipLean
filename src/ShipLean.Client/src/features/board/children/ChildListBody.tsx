import type { WorkItemTypeId } from '@shiplean/core/api/refs';
import type { Level } from '../../../routes';
import { boardTestIds as ids } from '../testIds';
import { AddChild } from './AddChild';
import { ChildRow } from './ChildRow';
import { useChildren } from './childrenApi';

// What a child list holds wherever it is drawn: the children with their states, then the ways to add one.
export function ChildListBody({
  projectId,
  projectKey,
  boardId,
  level,
  itemId,
  type,
}: {
  projectId: string;
  projectKey: string;
  boardId: string;
  level: Level;
  itemId: string;
  type: WorkItemTypeId;
}) {
  const children = useChildren(projectId, itemId, type);
  return (
    <>
      {children.isPending ? (
        <p data-testid={ids.childLoading} className="text-ink-muted">
          Loading…
        </p>
      ) : children.isError ? (
        <p data-testid={ids.childFailed} role="alert" className="text-alert">
          The children could not be loaded.
        </p>
      ) : children.data.length === 0 ? (
        <p data-testid={ids.childEmpty} className="text-ink-muted">
          Nothing below yet.
        </p>
      ) : (
        <ul className="grid gap-px">
          {children.data.map((child) => (
            <ChildRow key={child.id} child={child} projectKey={projectKey} />
          ))}
        </ul>
      )}
      <AddChild projectId={projectId} boardId={boardId} parentId={itemId} level={level} />
    </>
  );
}
