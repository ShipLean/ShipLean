import { Chip } from '../../../../components/Chip';
import { TypeIcon } from '../../../../components/TypeIcon';
import type { RetroCardDto } from '@shiplean/core/api/retros';
import { AuthorChip } from '../../../retroShared/AuthorChip';
import { FeatureChip } from '../../../retroShared/FeatureChip';

const roleLabels = { conductor: 'Conductor', subAgent: 'Sub-agent', person: 'Person' } as const;

/** The selected retro's title line: Feature marker, the item or the sprint, Rolled over, then who wrote it, their role and when. */
export function SelectedHeader({ card, bare }: { card: RetroCardDto; bare?: boolean }) {
  const { workItem, sprint } = card;
  return (
    <>
      {bare ? null : (
        <header className="flex flex-wrap items-center gap-1.5 text-[length:var(--text-sm)]">
          {workItem?.type.id === 'feature' ? <FeatureChip /> : null}
          {workItem ? (
            <>
              <TypeIcon type={workItem.type.id} />
              <span className="font-mono">{workItem.key}</span>
              <b className="text-[length:var(--text-md)]">{workItem.title}</b>
            </>
          ) : (
            <>
              <Chip>Sprint</Chip>
              <b className="text-[length:var(--text-md)]">{sprint?.name}</b>
            </>
          )}
          {card.partial ? <Chip tone="warn">Rolled over</Chip> : null}
        </header>
      )}
      <div className="flex flex-wrap items-center gap-2 text-[length:var(--text-sm)]">
        <AuthorChip author={card.author} />
        <Chip>{roleLabels[card.author.role]}</Chip>
        <span className="text-[length:var(--text-xs)] text-ink-muted">{card.createdAtLabel}</span>
      </div>
    </>
  );
}
