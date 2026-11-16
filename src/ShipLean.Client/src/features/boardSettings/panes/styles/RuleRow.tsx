import type { KeyboardEvent } from 'react';
import { SWATCHES, type StyleRuleJson, type Swatch } from '@shiplean/core/api/persisted';
import { CriteriaBuilder } from '../../../../criteria/CriteriaBuilder';
import type { PickLists } from '../../../../criteria/valueOptions';
import { TouchDragGhost } from '../../../../touch/TouchDragGhost';
import { useTouchShell } from '../../../../touch/useTouchShell';
import { boardSettingsTestIds as ids } from '../../testIds';
import { SWATCH_NAMES } from '../swatchNames';
import { dropLineClass, type DropLineProps } from '../useDropLine';
import { useRowTouchDrag } from '../useRowTouchDrag';
import { withCriteria } from './ruleEdits';

const iconButton =
  'inline-grid size-6 place-items-center rounded-control text-ink-muted hover:bg-sunken disabled:cursor-default disabled:opacity-35 touch:size-[var(--touch-target)]';
const formatButton =
  'inline-grid size-6 place-items-center rounded-control border border-edge text-[length:var(--text-sm)] aria-pressed:bg-signal-soft aria-pressed:text-ink disabled:opacity-50 touch:order-2 touch:size-[var(--touch-target)]';

/**
 * One style rule: name, card colour, title bold / italic / underline, enabled, and the criteria that pick its cards.
 * At touch the head wraps to two lines (⠿ · name · colour · ✕, then B I U · Enabled) and the grip is a long-press drag.
 */
export function RuleRow({
  rule,
  lists,
  readOnly,
  onChange,
  onDelete,
  onMove,
  onDragStart,
  onDrop,
  onTouchDrop,
  dropLine,
  onTouchOver,
  onTouchDragEnd,
}: DropLineProps & {
  rule: StyleRuleJson;
  lists: PickLists;
  readOnly: boolean;
  onChange(next: StyleRuleJson): void;
  onDelete(): void;
  /** -1 up, 1 down. */
  onMove(step: number): void;
  onDragStart(): void;
  onDrop(): void;
  /** At touch: the grip was held, dragged and let go over the rule with this id (null: off any rule). */
  onTouchDrop(targetId: string | null): void;
}) {
  const { touch } = useTouchShell();
  const drag = useRowTouchDrag({ onOver: onTouchOver, onDrop: onTouchDrop, onEnd: onTouchDragEnd });
  const keyDown = (event: KeyboardEvent) => {
    if (!event.altKey) return;
    if (event.key === 'ArrowUp') onMove(-1);
    else if (event.key === 'ArrowDown') onMove(1);
    else return;
    event.preventDefault();
  };
  return (
    <div
      data-testid={ids.styleRow}
      data-drop-target={rule.id}
      className={`grid gap-2 rounded-panel border border-edge p-2.5 ${rule.enabled ? 'bg-panel' : 'bg-sunken'} ${drag.lifted ? 'opacity-40' : ''} ${dropLineClass(dropLine)}`}
      onDragOver={(event) => event.preventDefault()}
      onDrop={onDrop}
    >
      <div className="flex items-center gap-2 touch:flex-wrap">
        <button
          type="button"
          data-testid={ids.styleGrip}
          draggable={!readOnly && !touch}
          disabled={readOnly}
          aria-label={`Move rule ${rule.name} (Alt+Up or Alt+Down)`}
          title="Drag, or press Alt+Up or Alt+Down"
          className={`${iconButton} cursor-grab`}
          onKeyDown={keyDown}
          onDragStart={onDragStart}
          {...(touch && !readOnly ? drag.props : {})}
        >
          ⠿
        </button>
        {drag.lifted ? (
          <TouchDragGhost x={drag.x} y={drag.y}>
            <div className="px-3 py-2 text-[length:var(--text-sm)]">{rule.name}</div>
          </TouchDragGhost>
        ) : null}
        <input
          data-testid={ids.styleName}
          className="field h-[26px] min-w-20 flex-1 touch:h-[var(--touch-target)]"
          aria-label="Rule name"
          value={rule.name}
          readOnly={readOnly}
          onChange={(event) => onChange({ ...rule, name: event.target.value })}
        />
        <select
          data-testid={ids.styleColour}
          className="field h-[26px] touch:h-[var(--touch-target)] touch:max-w-32"
          aria-label={`Card colour for ${rule.name}`}
          value={rule.colour ?? ''}
          disabled={readOnly}
          style={{
            borderLeft: `4px solid ${rule.colour ? `var(--color-${rule.colour})` : 'transparent'}`,
          }}
          onChange={(event) =>
            onChange({ ...rule, colour: (event.target.value || null) as Swatch | null })
          }
        >
          <option value="">No colour</option>
          {SWATCHES.map((swatch) => (
            <option key={swatch} value={swatch}>
              {SWATCH_NAMES[swatch]}
            </option>
          ))}
        </select>
        {/* At touch this breaks the head after ✕; B then indents by touch:ml-10 (the 32px grip and the 0.5rem gap) to sit under the name. */}
        <span aria-hidden="true" className="hidden touch:order-1 touch:block touch:basis-full" />
        <button
          type="button"
          data-testid={ids.styleBold}
          aria-pressed={rule.bold}
          aria-label="Bold title"
          disabled={readOnly}
          className={`${formatButton} font-bold touch:ml-10`}
          onClick={() => onChange({ ...rule, bold: !rule.bold })}
        >
          B
        </button>
        <button
          type="button"
          data-testid={ids.styleItalic}
          aria-pressed={rule.italic}
          aria-label="Italic title"
          disabled={readOnly}
          className={`${formatButton} italic`}
          onClick={() => onChange({ ...rule, italic: !rule.italic })}
        >
          I
        </button>
        <button
          type="button"
          data-testid={ids.styleUnderline}
          aria-pressed={rule.underline}
          aria-label="Underlined title"
          disabled={readOnly}
          className={`${formatButton} underline`}
          onClick={() => onChange({ ...rule, underline: !rule.underline })}
        >
          U
        </button>
        <label className="inline-flex items-center gap-1 text-[length:var(--text-sm)] touch:order-2 touch:ml-auto touch:min-h-[var(--touch-target)]">
          <input
            type="checkbox"
            data-testid={ids.styleEnabled}
            checked={rule.enabled}
            disabled={readOnly}
            onChange={(event) => onChange({ ...rule, enabled: event.target.checked })}
          />
          Enabled
        </label>
        <button
          type="button"
          data-testid={ids.styleDelete}
          disabled={readOnly}
          aria-label={`Delete rule ${rule.name}`}
          title="Delete rule"
          className={iconButton}
          onClick={onDelete}
        >
          ✕
        </button>
      </div>
      <div data-testid={ids.styleCriteria}>
        <CriteriaBuilder
          value={rule.criteria}
          onChange={(criteria) => onChange(withCriteria(rule, criteria))}
          {...lists}
        />
      </div>
    </div>
  );
}
