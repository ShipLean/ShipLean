import type { Level } from '../../../routes';
import { MoreButton } from '../../../touch/MoreButton';
import { LanesSwitch } from '../lanes/LanesSwitch';
import { boardTestIds as ids } from '../testIds';
import { CardsSwitch } from './CardsSwitch';
import { useOpenBoardSettings } from './SettingsOpener';

const heading =
  'px-2 pt-1 text-[length:var(--text-2xs)] font-semibold tracking-wide text-ink-muted uppercase';

// The touch toolbar's ⋯ More (SHIP-719 D5): the K5 MoreButton with the lane choice (Swimlanes, or Lanes on Features) and
// Full / Compact cards in its children slot, and Board settings as its one action row (it opens today's dialog).
export function BoardMoreSheet({
  level,
  laneCount,
  lanesValue,
  onChooseLanes,
  compact,
  onCompact,
}: {
  level: Level;
  laneCount: number;
  lanesValue: string | null;
  onChooseLanes: (value: string) => void;
  compact: boolean;
  onCompact: (compact: boolean) => void;
}) {
  const openSettings = useOpenBoardSettings();
  return (
    <MoreButton actions={[{ id: 'settings', label: 'Board settings', onSelect: openSettings }]}>
      {(close) => (
        <div data-testid={ids.moreSheet} className="grid gap-1">
          {level === 'epics' ? null : (
            <>
              <div className={heading}>{level === 'features' ? 'Lanes' : 'Swimlanes'}</div>
              <LanesSwitch
                level={level}
                laneCount={laneCount}
                value={lanesValue}
                onChoose={onChooseLanes}
                onPicked={close}
              />
            </>
          )}
          <div className={heading}>Cards</div>
          <CardsSwitch compact={compact} onCompact={onCompact} onPicked={close} />
          <div className={heading}>Board</div>
        </div>
      )}
    </MoreButton>
  );
}
