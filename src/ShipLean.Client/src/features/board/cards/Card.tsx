import type { ComponentProps } from 'react';
import { TypeIcon } from '../../../components/TypeIcon';
import type { BoardViewDto, CardDto } from '../../../api/views';
import type { WorkItemTypeId } from '../../../api/refs';
import type { Level } from '../../../routes';
import { ItemLink } from '../../itemPeek/ItemLink';
import { boardTestIds as ids } from '../testIds';
import { DropLine } from './DropLine';
import { CardAssignee } from './CardAssignee';
import { CardExtraFields } from './CardExtraFields';
import { CardOffer } from './CardOffer';
import { CardParent } from './CardParent';
import { CardPoints } from './CardPoints';
import { CardRollups } from './CardRollups';
import { CardTags } from './CardTags';
import { ruleCardStyle, ruleTitleStyle } from './cardStyle';

/** The board settings a card is drawn by. */
export type CardSettings = Pick<BoardViewDto, 'styleRules' | 'cardFields' | 'annotations'>;

export interface CardProps {
  card: CardDto;
  /** Unused since titles became ItemLinks (they read the open route's project); kept so callers stay unchanged. */
  projectKey?: string;
  level: Level;
  /** Compact: key, points, avatar and one line of title; Features and Epics keep their roll-ups. */
  compact: boolean;
  settings: CardSettings;
  /** The card sits in the board's last column: its title is muted. */
  inDone: boolean;
  /** Props of the card's element: a wrapper that drags or moves the card gives its ref, handlers and transform. */
  frame?: ComponentProps<'article'>;
  /** A teammate just moved the card here: it flashes. */
  flashing?: boolean;
  /** A dragged card is over this one: a line above it shows where the dragged card will land. */
  dropBefore?: boolean;
  /** The "Move to Done" offer was pressed (shown when the card's children are all closed). */
  onMoveToDone?: () => void;
  /** A roll-up was pressed: it becomes a button. */
  onRollupClick?: (type: WorkItemTypeId) => void;
}

// One card, full or compact. It draws only: dragging, keyboard moves and menus belong to whatever wraps it.
export function Card({
  card,
  level,
  compact,
  settings,
  inDone,
  frame,
  flashing = false,
  dropBefore = false,
  onMoveToDone,
  onRollupClick,
}: CardProps) {
  const fields = settings.cardFields.find((setting) => setting.type === card.type.id);
  const rule = settings.styleRules.find((candidate) => candidate.id === card.styleRuleId) ?? null;
  const showKey = fields?.showKey ?? true;
  const showAssignee = fields?.showAssignee ?? true;
  const showTags = fields?.showTags ?? true;
  const showPoints = fields?.showPoints ?? true;
  return (
    <article
      {...frame}
      data-testid={ids.card}
      data-key={card.key}
      data-item-id={card.id}
      data-revision={card.revision}
      data-compact={compact}
      data-flashing={flashing}
      data-rule={card.styleRuleId ?? undefined}
      className={`relative grid gap-1 rounded-control border border-edge bg-panel px-2 py-1.5 text-[length:var(--text-sm)] ${
        flashing ? 'board-card-flash' : ''
      }`}
      style={{ ...ruleCardStyle(rule), ...frame?.style }}
    >
      {dropBefore ? <DropLine above /> : null}
      <div className="flex min-w-0 items-center gap-1.5">
        <TypeIcon type={card.type.id} />
        {showKey ? (
          <span data-testid={ids.cardKey} className="text-[length:var(--text-2xs)] text-ink-muted">
            {card.key}
          </span>
        ) : null}
        {showPoints ? <CardPoints card={card} level={level} /> : null}
        {compact && showAssignee ? <CardAssignee card={card} /> : null}
      </div>
      <ItemLink
        testId={ids.cardTitle}
        itemKey={card.key}
        className={`leading-[1.35] font-medium ${compact ? 'line-clamp-1' : 'line-clamp-2'} ${
          inDone ? 'text-ink-muted' : 'text-ink'
        }`}
        style={ruleTitleStyle(rule)}
      >
        {card.title}
      </ItemLink>
      {compact ? null : (
        <>
          {/* Parent, roll-ups and assignee share a line; a line with nothing left in it is hidden, so the card closes up. */}
          <div className="flex min-w-0 items-center gap-1.5 empty:hidden">
            {card.parent ? <CardParent parent={card.parent} /> : null}
            <CardRollups
              rollups={card.rollups}
              annotations={settings.annotations}
              inDone={inDone}
              level={level}
              onRollupClick={onRollupClick}
            />
            {showAssignee ? (
              <span className="ml-auto">
                <CardAssignee card={card} />
              </span>
            ) : null}
          </div>
          <CardExtraFields fields={card.extraFields} />
          {showTags && card.tags.length > 0 ? (
            <div className="flex flex-wrap items-center gap-1">
              <CardTags tags={card.tags} />
            </div>
          ) : null}
        </>
      )}
      {compact && level !== 'stories' ? (
        <CardRollups
          rollups={card.rollups}
          annotations={settings.annotations}
          inDone={inDone}
          level={level}
          onRollupClick={onRollupClick}
        />
      ) : null}
      {card.offerDone && !inDone ? <CardOffer card={card} onMoveToDone={onMoveToDone} /> : null}
    </article>
  );
}
