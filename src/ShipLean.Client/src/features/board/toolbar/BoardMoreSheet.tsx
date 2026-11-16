import type { Level } from '../../../routes';
import { MoreButton } from '../../../touch/MoreButton';
import { LanesSwitch } from '../lanes/LanesSwitch';
import { boardTestIds as ids } from '../testIds';
import { CardsSwitch } from './CardsSwitch';
import { useOpenBoardSettings } from './SettingsOpener';

const heading =
  'px-2 pt-1 text-[length:var(--text-2xs)] font-semibold tracking-wide text-ink-muted uppercase';

// The touch toolbar's ⋯ More (SHIP-719 D5): the bordered (K1) K5 MoreButton with the lane choice (Swimlanes, or Lanes on Features) and
// Cards as a Full | Compact segmented control in its children slot, and Board settings as its one action row (it opens today's dialog).
export function BoardMoreSheet({
  level,
  laneCount,
  laneNames,
  lanesValue,
  onChooseLanes,
  compact,
  onCompact,
}: {
  level: Level;
  laneCount: number;
  laneNames: string[];
  lanesValue: string | null;
  onChooseLanes: (value: string) => void;
  compact: boolean;
  onCompact: (compact: boolean) => void;
}) {
  const openSettings = useOpenBoardSettings();
  return (
    <MoreButton
      label="Board"
      variant="bordered"
      footer="Lanes and card size are yours alone; Board settings change the board for everyone."
      actions={[
        {
          id: 'settings',
          label: 'Board settings',
          group: 'Settings',
          icon: '⚙',
          detail: 'columns, WIP, lanes, styles',
          trailing: 'chevron',
          onSelect: openSettings,
        },
      ]}
    >
      {(close) => (
        <div data-testid={ids.moreSheet} className="grid gap-1">
          {level === 'epics' ? null : (
            <>
              {level === 'features' ? <div className={heading}>Lanes</div> : null}
              <LanesSwitch
                level={level}
                laneCount={laneCount}
                laneNames={laneNames}
                value={lanesValue}
                onChoose={onChooseLanes}
                onPicked={close}
              />
            </>
          )}
          <CardsSwitch compact={compact} onCompact={onCompact} onPicked={close} />
        </div>
      )}
    </MoreButton>
  );
}
