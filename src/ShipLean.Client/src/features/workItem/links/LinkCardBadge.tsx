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

const matchReasons: Record<AutoLinkDto['matchedOn'], string> = {
  title: 'the key is in the title',
  branch: 'the key is in the branch name',
  body: 'the description names it or its Feature',
};

// The pull or merge request's state as its webhook last reported it, with the source and when in the tooltip, and
// "auto" when the webhook added the link because the title, branch or description named the item.
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
          title={`Linked by the ${providers[autoLinked.via]} webhook: ${matchReasons[autoLinked.matchedOn]}`}
          className="shrink-0 text-[length:var(--text-2xs)] text-ink-muted"
        >
          auto
        </span>
      ) : null}
    </>
  );
}
