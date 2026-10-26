import { useState } from 'react';
import type { BoardSettingsJson, StyleRuleJson } from '../../../api/persisted';
import type { PaneProps } from '../../../components/SettingsDialog/types';
import type { BoardSettingsContext } from '../panes';
import { boardSettingsTestIds as ids } from '../testIds';
import { useLanePickLists } from './swimlanes/useLanePickLists';
import { blankRule, moveRule, replaceRule } from './styles/ruleEdits';
import { RuleRow } from './styles/RuleRow';
import { StylePreview } from './styles/StylePreview';

/** Ordered style rules: a card takes the style of the first enabled rule it matches. */
export function StylesPane({
  draft,
  onChange,
  readOnly,
  context,
}: PaneProps<BoardSettingsJson, BoardSettingsContext>) {
  const lists = useLanePickLists(context.projectId, context.states);
  const [dragFrom, setDragFrom] = useState<number>();
  const setRules = (styleRules: StyleRuleJson[]) => onChange({ ...draft, styleRules });
  return (
    <div data-testid={ids.stylesPane} className="grid content-start gap-3">
      <p className="m-0 text-[length:var(--text-xs)] text-ink-muted">
        Style rules make cards that need attention stand out. When a card matches more than one
        rule, the first rule wins. Drag the grip to reorder.
      </p>
      <div className="grid grid-cols-[minmax(0,1fr)_minmax(0,16rem)] items-start gap-3">
        <div className="grid gap-2">
          <div>
            <button
              type="button"
              data-testid={ids.stylesAdd}
              disabled={readOnly}
              className="h-7 rounded-control border border-edge bg-panel px-3 text-[length:var(--text-sm)] text-ink disabled:opacity-50"
              onClick={() => setRules([...draft.styleRules, blankRule(lists)])}
            >
              + Add styling rule
            </button>
          </div>
          {draft.styleRules.length === 0 ? (
            <p data-testid={ids.stylesEmpty} className="m-0 text-ink-muted">
              No style rules. Cards use the plain style.
            </p>
          ) : null}
          {draft.styleRules.map((rule, index) => (
            <RuleRow
              key={rule.id}
              rule={rule}
              lists={lists}
              readOnly={readOnly}
              onChange={(next) => setRules(replaceRule(draft.styleRules, next))}
              onDelete={() => setRules(draft.styleRules.filter((r) => r.id !== rule.id))}
              onMove={(step) => setRules(moveRule(draft.styleRules, index, index + step))}
              onDragStart={() => setDragFrom(index)}
              onDrop={() => {
                if (dragFrom !== undefined) setRules(moveRule(draft.styleRules, dragFrom, index));
                setDragFrom(undefined);
              }}
            />
          ))}
        </div>
        <StylePreview draft={draft} level={context.level} captions testId={ids.stylesPreview} />
      </div>
    </div>
  );
}
