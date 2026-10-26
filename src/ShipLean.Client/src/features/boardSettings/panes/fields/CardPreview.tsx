import type { BoardLevel } from '../../../../api/boards';
import type { BoardSettingsJson, CardFieldSettingJson } from '../../../../api/persisted';
import { boardSettingsTestIds as ids } from '../../testIds';
import { BoardCardPreview } from './BoardCardPreview';
import { styledOutcome } from '../styles/styleSamples';
import { previewCard, sampleRuleValues } from './samplePreview';

/** The board's own card, drawn from a sample with the draft's settings for one type; it follows every toggle, and the style rules and tag colours apply. */
export function CardPreview({
  setting,
  draft,
  level,
}: {
  setting: CardFieldSettingJson;
  draft: BoardSettingsJson;
  level: BoardLevel;
}) {
  const { card } = styledOutcome(
    previewCard(setting),
    sampleRuleValues(setting.type, 2),
    draft,
    '',
  );
  return (
    <aside className="grid content-start gap-2">
      <span className="text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase">
        Preview · {setting.type}
      </span>
      <div data-testid={ids.fieldsPreview}>
        <BoardCardPreview
          card={card}
          level={level}
          cardFields={[setting]}
          styleRules={draft.styleRules}
        />
      </div>
      <p className="m-0 text-[length:var(--text-xs)] text-ink-muted">
        {card.key} has no sprint and nobody assigned. Turn on Show empty fields to see the empty
        extra fields; the points and the assignee always show, as on the board. Style rules and tag
        colours apply too.
      </p>
    </aside>
  );
}
