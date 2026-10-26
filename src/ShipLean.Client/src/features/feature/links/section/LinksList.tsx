import type { LinkCardsDto } from '../../../../api/linkCards';
import { LinkGroup } from './LinkGroup';
import { WorkItemLinkRow } from './WorkItemLinkRow';

// Work item rows, then URL cards grouped Code and Pages.
export function LinksList({
  cards,
  projectKey,
  projectId,
  itemKey,
}: {
  cards: LinkCardsDto;
  projectKey: string;
  projectId: string;
  itemKey: string;
}) {
  return (
    <div className="grid gap-3">
      {cards.workItems.length > 0 ? (
        <div className="grid gap-1">
          <h3 className="m-0 text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase">
            Work items
          </h3>
          <ul className="m-0 grid list-none rounded-control border border-edge bg-panel p-0">
            {cards.workItems.map((row) => (
              <WorkItemLinkRow key={row.id} row={row} projectKey={projectKey} />
            ))}
          </ul>
        </div>
      ) : null}
      <LinkGroup
        group="code"
        title="Code"
        cards={cards.code}
        projectId={projectId}
        itemKey={itemKey}
      />
      <LinkGroup
        group="page"
        title="Pages"
        cards={cards.pages}
        projectId={projectId}
        itemKey={itemKey}
      />
    </div>
  );
}
