import type { BoardLevel } from '../../../../api/boards';
import type { BoardSettingsJson } from '../../../../api/persisted';
import { boardSettingsTestIds as ids } from '../../testIds';
import { fieldLabel } from '../fields/fieldEdits';
import { BoardCardPreview } from '../fields/BoardCardPreview';
import { sampleOutcomes, unpreviewableRules, type SampleOutcome } from './styleSamples';
import { useViewer } from './useViewer';

function caption({ matched }: SampleOutcome) {
  if (matched.length === 0) return 'No rule matches: plain card';
  const [first, ...others] = matched;
  return others.length === 0 ? (
    <b>{first.name}</b>
  ) : (
    <>
      <b>{first.name}</b> wins. Also matches {others.map((r) => r.name).join(', ')}
    </>
  );
}

/** Sample cards drawn by the board's own card under the draft's rules and tag colours, each saying which rule it got. */
export function StylePreview({
  draft,
  level,
  captions,
  testId,
}: {
  draft: BoardSettingsJson;
  level: BoardLevel;
  captions: boolean;
  testId: string;
}) {
  const viewer = useViewer();
  if (viewer === undefined) return null;
  const unpreviewable = captions ? unpreviewableRules(draft) : [];
  return (
    <aside className="grid content-start gap-2">
      <span className="text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase">
        Preview{captions ? ' · first match wins' : ''}
      </span>
      <div data-testid={testId} className="grid gap-2">
        {sampleOutcomes(draft, level, viewer).map((outcome) => (
          <div key={outcome.card.id} className="grid gap-1">
            {captions ? (
              <p
                data-testid={ids.stylesPreviewCaption}
                className="m-0 text-[length:var(--text-xs)] text-ink-muted"
              >
                {caption(outcome)}
              </p>
            ) : null}
            <BoardCardPreview
              card={outcome.card}
              level={level}
              cardFields={draft.cardFields}
              styleRules={draft.styleRules}
            />
          </div>
        ))}
      </div>
      {unpreviewable.map(({ rule, fields }) => (
        <p
          key={rule.id}
          data-testid={ids.stylesPreviewUnpreviewable}
          className="m-0 text-[length:var(--text-xs)] text-ink-muted"
        >
          Can't preview {rule.name}: it uses {fields.map((f) => fieldLabel(f)).join(', ')}.
        </p>
      ))}
    </aside>
  );
}
