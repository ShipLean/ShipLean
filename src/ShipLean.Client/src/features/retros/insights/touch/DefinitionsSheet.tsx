import { useLayoutEffect, useRef, type ReactNode } from 'react';
import { Chip, type ChipTone } from '../../../../components/Chip';
import type { RetroInsightsMode } from '@shiplean/core/api/retros';
import { Sheet } from '../../../../touch/Sheet';
import { retrosTestIds as ids } from '../../testIds';
import {
  STATUS_CHIPS,
  VERDICT_CHIPS,
  noSprintWording,
  statusHeadIntro,
  statusWording,
  unthemedWording,
  verdictHeadIntro,
  verdictWording,
  type DefsEntry,
} from '../tips/tipWording';

const GROUP =
  'pt-2.5 text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase';
const HINT = 'm-0 text-[length:var(--text-sm)] text-ink-muted';

function Entry({
  id,
  entry,
  chip,
  children,
}: {
  id: string;
  entry: DefsEntry | null;
  chip: ReactNode;
  children: ReactNode;
}) {
  return (
    <div
      data-testid={ids.retroInsightsDefsEntry}
      data-entry={id}
      data-highlighted={entry === id}
      className="-mx-2 grid grid-cols-[120px_minmax(0,1fr)] items-start gap-2 rounded-control p-2 text-[length:var(--text-sm)] data-[highlighted=true]:bg-canvas data-[highlighted=true]:ring-1 data-[highlighted=true]:ring-signal"
    >
      <span className="justify-self-start">{chip}</span>
      <span>{children}</span>
    </div>
  );
}

const tonedChip = (label: string, tone: ChipTone) => <Chip tone={tone}>{label}</Chip>;

/**
 * What these mean (D5): every status and verdict, No sprint and Unthemed. The entry the address names is highlighted and
 * scrolled into view; a group entry (`status`, `verdicts`) scrolls to its group.
 */
export function DefinitionsSheet({
  entry,
  mode,
  onClose,
}: {
  entry: DefsEntry | null;
  mode: RetroInsightsMode;
  onClose: () => void;
}) {
  const body = useRef<HTMLDivElement>(null);
  useLayoutEffect(() => {
    const target = body.current?.querySelector(
      `[data-entry="${entry ?? ''}"], [data-group="${entry ?? ''}"]`,
    );
    target?.scrollIntoView({ block: 'center' });
  }, [entry]);
  return (
    <Sheet open title="What these mean" onClose={onClose}>
      <div ref={body} className="grid gap-1">
        <div data-group="status" className={GROUP}>
          Status
        </div>
        <p className={HINT}>{statusHeadIntro(mode)}</p>
        {STATUS_CHIPS.map((s) => (
          <Entry key={s.id} id={s.id} entry={entry} chip={tonedChip(s.label, s.tone)}>
            {statusWording(s.id, mode)}
          </Entry>
        ))}
        <div data-group="verdicts" className={GROUP}>
          Last change
        </div>
        <p className={HINT}>{verdictHeadIntro}</p>
        {VERDICT_CHIPS.map((v) => (
          <Entry key={v.id} id={v.id} entry={entry} chip={tonedChip(v.label, v.tone)}>
            {verdictWording(v.id, mode)}
          </Entry>
        ))}
        <div className={GROUP}>Also</div>
        {mode === 'sprint' ? (
          <Entry
            id="nosprint"
            entry={entry}
            chip={<span className="font-semibold">No sprint</span>}
          >
            {noSprintWording}
          </Entry>
        ) : null}
        <Entry id="unthemed" entry={entry} chip={<span className="font-semibold">Unthemed</span>}>
          {unthemedWording}
        </Entry>
      </div>
    </Sheet>
  );
}
