import { useNavigate } from 'react-router-dom';
import type { RetroItemDto } from '@shiplean/core/api/retros';
import { routePath } from '../../../routes';
import { MoreButton } from '../../../touch/MoreButton';
import type { SheetAction } from '../../../touch/ActionSheet';
import { useTouchShell } from '../../../touch/useTouchShell';
import { useRetroActionDialog } from '../../retroShared/actionDialog/useRetroActionDialog';
import { retrosTestIds as ids } from '../testIds';
import { ItemToggleGroup } from './ItemToggleGroup';
import type { RetroItemAddress } from './retroItemAddress';
import { compiledFrom } from './ItemRail';
import type { RailSheet } from './touch/ItemRailSheets';

const ALL = 'all';
const POINT_FILTERS = [
  { id: 'all', label: 'All points' },
  { id: 'open', label: 'To decide' },
];

/** The retro filter, the All points / To decide filter and + Action item, scoped to this item. */
export function ItemToolbar({
  projectKey,
  item,
  address,
  onRetro,
  onDecide,
  onSheet,
}: {
  projectKey: string;
  item: RetroItemDto;
  address: RetroItemAddress;
  onRetro: (retroId: string | undefined) => void;
  onDecide: (open: boolean) => void;
  onSheet: (sheet: RailSheet) => void;
}) {
  const { touch } = useTouchShell();
  const navigate = useNavigate();
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
  const addAction = (
    <button
      type="button"
      data-testid={ids.retroItemAddAction}
      className="cursor-pointer rounded-control bg-signal px-3 py-1 text-[length:var(--text-sm)] font-medium text-signal-ink touch:min-h-[var(--touch-target)]"
      onClick={() => dialog.open({ scope: { kind: 'item', itemId: item.item.id } })}
    >
      + Action item
    </button>
  );
  if (touch) {
    const actions: SheetAction[] = [
      { id: 'actions', label: 'Action items', onSelect: () => onSheet('actions') },
      ...(compiledFrom(item).length > 0
        ? [{ id: 'compiled', label: 'Compiled from', onSelect: () => onSheet('compiled') }]
        : []),
      ...(item.cards.length > 0
        ? [{ id: 'writers', label: 'Written by', onSelect: () => onSheet('writers') }]
        : []),
      {
        id: 'open-work-item',
        label: 'Open work item',
        onSelect: () =>
          navigate(routePath('workItem', { key: projectKey, itemKey: item.item.key })),
      },
    ];
    return (
      <div className="flex items-center gap-2">
        <select
          data-testid={ids.retroItemRetroSelect}
          aria-label="Retro"
          value={address.retro ?? ALL}
          className="h-[var(--touch-target)] min-w-0 flex-1 rounded-control border border-edge bg-panel px-2"
          onChange={(event) => onRetro(event.target.value === ALL ? undefined : event.target.value)}
        >
          {retros.map((retro) => (
            <option key={retro.id} value={retro.id}>
              {retro.label}
            </option>
          ))}
        </select>
        {addAction}
        <MoreButton actions={actions} />
      </div>
    );
  }
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
      {addAction}
    </div>
  );
}
