import type { BacklogRowDto } from '@shiplean/core/api/views';
import type { SheetAction } from '../../../touch/ActionSheet';
import { MoreButton } from '../../../touch/MoreButton';
import { useItemPeek } from '../../itemPeek/useItemPeek';
import { moveToEdgePlan } from '../bulk/movePlan';
import { useBulkRunner } from '../bulk/useBulkRun';
import { moveBottomIcon, moveTopIcon, openIcon } from '../toolbar/moreIcons';

/** The ⋯ on a Feature or Epic row at touch (SHIP-1626 D4): Open, Move to top and Move to bottom, as the bulk move the list uses. */
export function LevelRowMore({ row }: { row: BacklogRowDto }) {
  const peek = useItemPeek();
  const runner = useBulkRunner();
  const actions: SheetAction[] = [
    { id: 'open', label: 'Open', icon: openIcon, onSelect: () => peek.open(row.key) },
    {
      id: 'top',
      label: 'Move to top',
      icon: moveTopIcon,
      group: '',
      onSelect: () => void runner.run(moveToEdgePlan([row], 'first')),
    },
    {
      id: 'bottom',
      label: 'Move to bottom',
      icon: moveBottomIcon,
      group: '',
      onSelect: () => void runner.run(moveToEdgePlan([row], 'last')),
    },
  ];
  return <MoreButton actions={actions} label={`More for ${row.key}`} subtitle={row.title} />;
}
