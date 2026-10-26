import type { KeyboardEvent } from 'react';
import { SWATCHES, type StyleRuleJson, type Swatch } from '../../../../api/persisted';
import { CriteriaBuilder } from '../../../../criteria/CriteriaBuilder';
import type { PickLists } from '../../../../criteria/valueOptions';
import { boardSettingsTestIds as ids } from '../../testIds';
import { SWATCH_NAMES } from '../swatchNames';
import { withCriteria } from './ruleEdits';

const iconButton =
  'inline-grid size-6 place-items-center rounded-control text-ink-muted hover:bg-sunken disabled:cursor-default disabled:opacity-35';
const formatButton =
  'inline-grid size-6 place-items-center rounded-control border border-edge text-[length:var(--text-sm)] aria-pressed:bg-signal-soft aria-pressed:text-ink disabled:opacity-50';

/** One style rule: name, card colour, title bold / italic / underline, enabled, and the criteria that pick its cards. */
export function RuleRow({
  rule,
  lists,
  readOnly,
  onChange,
  onDelete,
  onMove,
  onDragStart,
  onDrop,
}: {
  rule: StyleRuleJson;
  lists: PickLists;
  readOnly: boolean;
  onChange(next: StyleRuleJson): void;
  onDelete(): void;
  /** -1 up, 1 down. */
  onMove(step: number): void;
  onDragStart(): void;
  onDrop(): void;
}) {
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
      className={`grid gap-2 rounded-panel border border-edge p-2.5 ${rule.enabled ? 'bg-panel' : 'bg-sunken'}`}
      onDragOver={(event) => event.preventDefault()}
      onDrop={onDrop}
    >
      <div className="flex items-center gap-2">
        <button
          type="button"
          data-testid={ids.styleGrip}
          draggable={!readOnly}
          disabled={readOnly}
          aria-label={`Move rule ${rule.name} (Alt+Up or Alt+Down)`}
          title="Drag, or press Alt+Up or Alt+Down"
          className={`${iconButton} cursor-grab`}
          onKeyDown={keyDown}
          onDragStart={onDragStart}
        >
          ⠿
        </button>
        <input
          data-testid={ids.styleName}
          className="field h-[26px] min-w-20 flex-1"
          aria-label="Rule name"
          value={rule.name}
          readOnly={readOnly}
          onChange={(event) => onChange({ ...rule, name: event.target.value })}
        />
        <select
          data-testid={ids.styleColour}
          className="field h-[26px]"
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
        <button
          type="button"
          data-testid={ids.styleBold}
          aria-pressed={rule.bold}
          aria-label="Bold title"
          disabled={readOnly}
          className={`${formatButton} font-bold`}
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
        <label className="inline-flex items-center gap-1 text-[length:var(--text-sm)]">
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
