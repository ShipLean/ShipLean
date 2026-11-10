import type { RetroItemDto } from '@shiplean/core/api/retros';
import { useRetroActionDialog } from '../../retroShared/actionDialog/useRetroActionDialog';
import { retrosTestIds as ids } from '../testIds';
import { ItemToggleGroup } from './ItemToggleGroup';
import type { RetroItemAddress } from './retroItemAddress';

const ALL = 'all';
const POINT_FILTERS = [
  { id: 'all', label: 'All points' },
  { id: 'open', label: 'To decide' },
];

/** The retro filter, the All points / To decide filter and + Action item, scoped to this item. */
export function ItemToolbar({
  item,
  address,
  onRetro,
  onDecide,
}: {
  item: RetroItemDto;
  address: RetroItemAddress;
  onRetro: (retroId: string | undefined) => void;
  onDecide: (open: boolean) => void;
}) {
  const dialog = useRetroActionDialog();
  const retros = [
    { id: ALL, label: 'All retros' },
    ...item.cards.map((card) => ({
      id: card.id,
      label: card.author.agentName
        ? `${card.author.agentName} · ${card.author.role}`
        : card.author.user.name,
    })),
  ];
  return (
    <div className="flex flex-wrap items-center gap-3">
      <ItemToggleGroup
        label="Retro"
        testId={ids.retroItemFilter}
        options={retros}
        value={address.retro ?? ALL}
        onChange={(id) => onRetro(id === ALL ? undefined : id)}
      />
      <ItemToggleGroup
        label="Points"
        testId={ids.retroToDecideFilter}
        options={POINT_FILTERS}
        value={address.decide ? 'open' : 'all'}
        onChange={(id) => onDecide(id === 'open')}
      />
      <span className="flex-1" />
      <button
        type="button"
        data-testid={ids.retroItemAddAction}
        className="cursor-pointer rounded-control bg-signal px-3 py-1 text-[length:var(--text-sm)] font-medium text-signal-ink"
        onClick={() => dialog.open({ scope: { kind: 'item', itemId: item.item.id } })}
      >
        + Action item
      </button>
    </div>
  );
}
