import type { ComponentProps } from 'react';
import { TypeIcon } from '../../../components/TypeIcon';
import type { BoardViewDto, CardDto } from '@shiplean/core/api/views';
import type { WorkItemTypeId } from '@shiplean/core/api/refs';
import type { Level } from '../../../routes';
import { useTouchShell } from '../../../touch/useTouchShell';
import { useOptionalCardActions } from './touch/CardActionsContext';
import { ChildListInline } from '../children/ChildListInline';
import { useOptionalChildList } from '../children/ChildListContext';
import { CardMore } from './touch/CardMore';
import { ItemLink } from '../../itemPeek/ItemLink';
import { boardTestIds as ids } from '../testIds';
import { DropLine } from './DropLine';
import { CardAssignee } from './CardAssignee';
import { HealthPill } from '../../feature/statusBand/HealthPill';
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
  /** The project the card's links open in; defaults to the open route's. A different key makes them plain full-page links. */
  projectKey?: string;
  /** The title is a link to the item (default); false draws it as plain text (a settings preview). */
  linkTitle?: boolean;
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
  projectKey,
  linkTitle = true,
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
  const { touch } = useTouchShell();
  // Inside a board only: a card drawn as a preview has no actions and no tap to open.
  const actions = useOptionalCardActions();
  // At touch widths this card's open child list is drawn inside it, under the roll-ups.
  const childList = useOptionalChildList();
  const openList = touch && childList?.open?.itemId === card.id ? childList.open : null;
  const rule = settings.styleRules.find((candidate) => candidate.id === card.styleRuleId) ?? null;
  const showKey = fields?.showKey ?? true;
  const showAssignee = fields?.showAssignee ?? true;
  const showTags = fields?.showTags ?? true;
  const showPoints = fields?.showPoints ?? true;
  const titleClass = `leading-[1.35] font-medium touch:min-h-[var(--touch-target)] ${compact ? 'line-clamp-1' : 'line-clamp-2'} ${
    inDone ? 'text-ink-muted' : 'text-ink'
  }`;
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
      className={`relative grid gap-1 paper paper-sheet rounded-control border border-edge bg-panel px-2 py-1.5 text-[length:var(--text-sm)] ${
        card.type.id === 'bug' ? 'paper-bug' : ''
      } ${flashing ? 'board-card-flash' : ''}`}
      style={{ ...ruleCardStyle(rule), ...frame?.style }}
      onClick={(event) => {
        frame?.onClick?.(event);
        // At touch widths a tap on the card (not on a link, a button or the actions) opens the item.
        if (!touch || !actions || event.defaultPrevented) return;
        // A sheet opened from the card is a portal: its clicks bubble here through React but are not on the card.
        if (!event.currentTarget.contains(event.target as Node)) return;
        if ((event.target as HTMLElement).closest('a, button, input, select, textarea')) return;
        // A tap on the open child list (between its links) belongs to the list, not the card.
        if ((event.target as HTMLElement).closest(`[data-testid="${ids.childList}"]`)) return;
        actions.openItem(card.key);
      }}
    >
      {dropBefore ? <DropLine above /> : null}
      <div className="flex min-w-0 items-center gap-1.5">
        <TypeIcon type={card.type.id} />
        {showKey ? (
          <span data-testid={ids.cardKey} className="text-[length:var(--text-2xs)] text-ink-muted">
            {card.key}
          </span>
        ) : null}
        {card.type.id === 'feature' ? (
          <HealthPill health={card.health} testId={ids.cardHealth} />
        ) : null}
        {/* At touch the spacer takes the free width, so what follows it (points, ⋯) sits at the right edge with or
            without points; CardPoints' own ml-auto no longer has to carry the ⋯ there. */}
        <span aria-hidden className="hidden min-w-0 flex-1 touch:block" />
        {showPoints ? <CardPoints card={card} level={level} /> : null}
        {compact && showAssignee ? <CardAssignee card={card} /> : null}
        {touch && actions ? <CardMore card={card} actions={actions} /> : null}
      </div>
      {linkTitle ? (
        <ItemLink
          testId={ids.cardTitle}
          itemKey={card.key}
          projectKey={projectKey}
          className={titleClass}
          style={ruleTitleStyle(rule)}
        >
          {card.title}
        </ItemLink>
      ) : (
        <span data-testid={ids.cardTitle} className={titleClass} style={ruleTitleStyle(rule)}>
          {card.title}
        </span>
      )}
      {compact ? null : (
        <>
          {/* Parent, roll-ups and assignee share a line; a line with nothing left in it is hidden, so the card closes up. */}
          <div className="flex min-w-0 items-center gap-1.5 empty:hidden">
            {card.parent ? <CardParent parent={card.parent} projectKey={projectKey} /> : null}
            <CardRollups
              rollups={card.rollups}
              annotations={settings.annotations}
              inDone={inDone}
              level={level}
              onRollupClick={onRollupClick}
              openType={touch && childList ? (openList?.type ?? null) : undefined}
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
          openType={touch && childList ? (openList?.type ?? null) : undefined}
        />
      ) : null}
      {openList && childList ? <ChildListInline open={openList} host={childList} /> : null}
      {card.offerDone && !inDone ? <CardOffer card={card} onMoveToDone={onMoveToDone} /> : null}
    </article>
  );
}
