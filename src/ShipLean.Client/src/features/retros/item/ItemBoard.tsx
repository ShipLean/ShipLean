import type { RetroItemDto, RetroPointDto } from '@shiplean/core/api/retros';
import { RetroBoard } from '../../retroShared/board/RetroBoard';
import type { RetroItemAddress } from './retroItemAddress';

/** The item's own retros as one board, narrowed by the address's retro and decide filters. */
export function ItemBoard({
  item,
  address,
  onAdded,
}: {
  item: RetroItemDto;
  address: RetroItemAddress;
  onAdded: (point: RetroPointDto) => void;
}) {
  return (
    <RetroBoard
      columns={item.categories}
      groupByTheme={false}
      filter={(point) =>
        (address.retro === undefined || point.retroId === address.retro) &&
        (!address.decide || point.decision === 'undecided')
      }
      note={{ showAuthor: true, showTheme: true, highlightedPointId: address.point }}
      add={{ kind: 'item', workItemId: item.item.id }}
      scope={{ kind: 'item', itemId: item.item.id }}
      onAdded={onAdded}
    />
  );
}
