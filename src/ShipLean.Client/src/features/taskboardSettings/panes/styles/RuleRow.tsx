import { useEffect, useRef, useState, type FormEvent, type KeyboardEvent } from 'react';
import { SWATCHES, type StyleRuleJson, type Swatch } from '../../../../api/persisted';
import type { CriteriaJson } from '../../../../api/persisted';
import { CriteriaBuilder } from '../../../../criteria/CriteriaBuilder';
import { checkCriteria } from '../../../../criteria/validate';
import type { PickLists } from '../../../../criteria/valueOptions';
import { taskboardSettingsTestIds as ids } from '../../testIds';
import { withCriteria } from '../../../boardSettings/panes/styles/ruleEdits';
import { useHeldRules } from './HeldRulesContext';

const iconButton =
  'inline-grid size-6 place-items-center rounded-control text-ink-muted hover:bg-sunken disabled:cursor-default disabled:opacity-35';
const formatButton =
  'inline-grid size-6 place-items-center rounded-control border border-edge text-[length:var(--text-sm)] aria-pressed:bg-signal-soft aria-pressed:text-ink disabled:opacity-50';

/**
 * One style rule: name, card colour, title bold / italic / underline, enabled, and the criteria that pick its cards.
 * Text is committed on blur or Enter, one change per edit. Criteria that are not valid yet stay here (with their errors shown)
 * and are committed once they pass the check, so a half-built rule never reaches the server.
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
  const held = useHeldRules();
  const [name, setName] = useState(rule.name);
  const [seenName, setSeenName] = useState(rule.name);
  if (rule.name !== seenName) {
    setSeenName(rule.name);
    setName(rule.name);
  }
  const [criteria, setCriteria] = useState(rule.criteria);
  const [seenCriteria, setSeenCriteria] = useState(rule.criteria);
  // Typed criteria that are valid and wait for blur or Enter.
  const [pending, setPending] = useState(false);
  const invalid = checkCriteria(criteria).length > 0;
  if (rule.criteria !== seenCriteria) {
    setSeenCriteria(rule.criteria);
    if (!invalid && !pending) setCriteria(rule.criteria);
  }
  const typing = useRef(false);
  useEffect(() => {
    held.report(rule.id, invalid);
    return () => held.report(rule.id, false);
  }, [held, rule.id, invalid]);

  const commitName = () => {
    if (name !== rule.name) onChange({ ...rule, name });
  };
  const commitCriteria = (next: CriteriaJson) => {
    setPending(false);
    onChange(withCriteria(rule, next));
  };
  const changeCriteria = (next: CriteriaJson) => {
    setCriteria(next);
    const typed = typing.current;
    typing.current = false;
    if (checkCriteria(next).length > 0) return;
    if (typed) setPending(true);
    else commitCriteria(next);
  };
  const flushCriteria = () => {
    if (pending && !invalid) commitCriteria(criteria);
  };
  const noteInput = (event: FormEvent) => {
    const target = event.target;
    typing.current =
      target instanceof HTMLInputElement && (target.type === 'text' || target.type === 'number');
  };
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
          value={name}
          readOnly={readOnly}
          onChange={(event) => setName(event.target.value)}
          onBlur={commitName}
          onKeyDown={(event) => {
            if (event.key === 'Enter') commitName();
          }}
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
              {swatch}
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
      <div
        data-testid={ids.styleCriteria}
        onInputCapture={noteInput}
        onClickCapture={() => {
          typing.current = false;
        }}
        onBlur={flushCriteria}
        onKeyDown={(event) => {
          if (event.key === 'Enter') flushCriteria();
        }}
      >
        <CriteriaBuilder
          value={criteria}
          onChange={changeCriteria}
          errors={checkCriteria(criteria).filter((error) => error.clauseIndex === null)}
          {...lists}
        />
      </div>
    </div>
  );
}
