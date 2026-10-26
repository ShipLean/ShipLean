import type { AutoLinkDto, LinkCardDto } from '../../../api/workItems';
import { workItemTestIds as ids } from '../testIds';

const stateLabels: Record<LinkCardDto['state'], string> = {
  open: 'Open',
  merged: 'Merged',
  closed: 'Closed',
};

const stateTints: Record<LinkCardDto['state'], string> = {
  open: 'bg-good/20',
  merged: 'bg-label-purple/22',
  closed: 'bg-alert/18',
};

const providers: Record<LinkCardDto['source'], string> = { github: 'GitHub', gitlab: 'GitLab' };

// The pull or merge request's state as its webhook last reported it, with the source and when in the tooltip, and
// "auto" when the webhook added the link because the item's key was in the title or branch.
export function LinkCardBadge({
  card,
  autoLinked,
}: {
  card: LinkCardDto;
  autoLinked: AutoLinkDto | null;
}) {
  return (
    <>
      <span
        data-testid={ids.linkCard}
        data-state={card.state}
        title={`${providers[card.source]} webhook · ${card.asOfLabel}${card.title ? ` · ${card.title}` : ''}`}
        className={`inline-flex h-4.5 shrink-0 items-center rounded-full px-1.5 text-[length:var(--text-2xs)] font-semibold ${stateTints[card.state]}`}
      >
        {stateLabels[card.state]}
      </span>
      {autoLinked ? (
        <span
          data-testid={ids.linkAutoLinked}
          title={`Linked by the ${providers[autoLinked.via]} webhook: the key is in the ${autoLinked.matchedOn === 'title' ? 'title' : 'branch name'}`}
          className="shrink-0 text-[length:var(--text-2xs)] text-ink-muted"
        >
          auto
        </span>
      ) : null}
    </>
  );
}
