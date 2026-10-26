import type { BoardLevel } from '../../../../api/boards';
import type { BoardSettingsJson } from '../../../../api/persisted';
import type { CardDto } from '../../../../api/views';
import { Card } from '../../../board/cards/Card';
import { PREVIEW_PROJECT_KEY } from './samplePreview';

const noAnnotations = { tasks: false, stories: false, bugs: false, features: false };

/** The board's own card, drawn as the board would draw it for these settings (a card whose `styleRuleId` names one of the style rules is styled by it). */
export function BoardCardPreview({
  card,
  level,
  cardFields,
  styleRules,
}: {
  card: CardDto;
  level: BoardLevel;
  cardFields: BoardSettingsJson['cardFields'];
  styleRules: BoardSettingsJson['styleRules'];
}) {
  return (
    <Card
      card={card}
      projectKey={PREVIEW_PROJECT_KEY}
      level={level === 'taskboard' ? 'stories' : level}
      compact={false}
      settings={{ styleRules, cardFields, annotations: noAnnotations }}
      inDone={false}
    />
  );
}
