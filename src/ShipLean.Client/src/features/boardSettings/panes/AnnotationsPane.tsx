import type { BoardSettingsJson } from '../../../api/persisted';
import { TypeIcon } from '../../../components/TypeIcon';
import type { PaneProps } from '../../../components/SettingsDialog/types';
import type { BoardSettingsContext } from '../panes';
import { boardSettingsTestIds as ids } from '../testIds';
import { AnnotationPreview } from './annotations/AnnotationPreview';
import { annotationRowsFor, hasEffect } from './annotations/annotationRows';

/** Child roll-ups on cards: how many children are done out of how many. */
export function AnnotationsPane({
  draft,
  onChange,
  readOnly,
  context,
}: PaneProps<BoardSettingsJson, BoardSettingsContext>) {
  const { level } = context;
  return (
    <div data-testid={ids.annotationsPane} className="grid content-start gap-3">
      <p className="m-0 text-[length:var(--text-xs)] text-ink-muted">
        Child roll-ups on cards: how many children are done out of how many. Click a roll-up on the
        board to see the children without leaving it.
      </p>
      <div className="grid grid-cols-[minmax(0,1fr)_minmax(0,16rem)] items-start gap-3">
        <fieldset className="m-0 grid gap-1.5 rounded-panel border border-edge p-3">
          <legend className="px-1 text-[length:var(--text-sm)] font-semibold">
            Roll-ups on cards
          </legend>
          {annotationRowsFor(level).map((row) => {
            const effect = hasEffect(level, row.key);
            return (
              <label
                key={row.key}
                className={`flex items-center gap-2 text-[length:var(--text-sm)] ${effect ? '' : 'text-ink-muted'}`}
              >
                <input
                  type="checkbox"
                  data-testid={row.testId}
                  checked={effect && draft.annotations[row.key]}
                  disabled={readOnly || !effect}
                  onChange={(event) =>
                    onChange({
                      ...draft,
                      annotations: { ...draft.annotations, [row.key]: event.target.checked },
                    })
                  }
                />
                <TypeIcon type={row.type} />
                {row.label}
                <span className="text-ink-muted">
                  · {effect ? row.hint : 'no effect on this board'}
                </span>
              </label>
            );
          })}
        </fieldset>
        <AnnotationPreview level={level} annotations={draft.annotations} />
      </div>
    </div>
  );
}
