import type { CSSProperties, HTMLAttributes, Ref } from 'react';
import type { CardDto, TaskboardViewDto } from '../../../api/views';
import { Avatar } from '../../../components/Avatar';
import { Chip } from '../../../components/Chip';
import { TypeIcon } from '../../../components/TypeIcon';
import { ItemLink } from '../../itemPeek/ItemLink';
import { CardExtraFields } from '../../board/cards/CardExtraFields';
import { taskboardTestIds as ids } from '../testIds';

// What a task card shows when the board has no setting for tasks.
const ALL_FIELDS = {
  showKey: true,
  showAssignee: true,
  showTags: true,
  showPoints: true,
  showEmptyFields: false,
  extraFields: ['parent'] as string[],
};

/**
 * What a task card looks like: the same markup for the card in its cell and for the copy that follows the pointer while it is
 * dragged. The copy (`preview`) carries no test ids, so a test never sees two of a card. The parent line shows when the rows
 * are people (the card's story is not otherwise on screen) and the task's parent extra is on.
 */
export function TaskCardFace({
  card,
  view,
  showParent,
  preview = false,
  articleRef,
  articleProps,
  style,
  pickUp,
}: {
  card: CardDto;
  view: Pick<TaskboardViewDto, 'styleRules' | 'cardFields'>;
  showParent: boolean;
  preview?: boolean;
  articleRef?: Ref<HTMLElement>;
  articleProps?: HTMLAttributes<HTMLElement>;
  style?: CSSProperties;
  /** The Pick up button, or null when the card does not offer it. */
  pickUp?: (() => void) | null;
}) {
  const fields = view.cardFields.find((f) => f.type === 'task') ?? ALL_FIELDS;
  const rule = view.styleRules.find((r) => r.id === card.styleRuleId);
  const id = (testId: string) => (preview ? undefined : testId);
  return (
    <article
      ref={articleRef}
      {...articleProps}
      role="group"
      aria-label={`${card.title}, ${card.state.name}`}
      data-testid={id(ids.card)}
      data-key={card.key}
      data-state={card.state.id}
      data-style-rule={rule?.id}
      className="group relative grid gap-1 rounded-panel border border-edge bg-panel p-2 shadow-panel"
      style={{
        ...style,
        ...(rule?.colour ? { borderLeft: `3px solid var(--color-${rule.colour})` } : {}),
        fontWeight: rule?.bold ? 700 : undefined,
        fontStyle: rule?.italic ? 'italic' : undefined,
        textDecoration: rule?.underline ? 'underline' : undefined,
      }}
    >
      {showParent && fields.extraFields.includes('parent') ? (
        <span
          data-testid={id(ids.cardParent)}
          className="flex items-center gap-1 text-[length:var(--text-xs)] text-ink-muted"
        >
          {card.parent ? (
            <>
              <TypeIcon type={card.parent.type.id} />
              {preview ? (
                card.parent.title
              ) : (
                <ItemLink itemKey={card.parent.key} className="hover:underline">
                  {card.parent.title}
                </ItemLink>
              )}
            </>
          ) : (
            'No parent'
          )}
        </span>
      ) : null}
      <span className="flex items-start gap-1.5 text-[length:var(--text-sm)] text-ink">
        <TypeIcon type="task" />
        <span data-testid={id(ids.cardTitle)}>
          {preview ? (
            card.title
          ) : (
            <ItemLink itemKey={card.key} className="text-inherit no-underline hover:underline">
              {card.title}
            </ItemLink>
          )}
        </span>
      </span>
      <span className="flex flex-wrap items-center gap-1.5 text-[length:var(--text-xs)] text-ink-muted">
        {fields.showKey ? (
          <span data-testid={id(ids.cardKey)} className="whitespace-nowrap">
            {card.key}
          </span>
        ) : null}
        {fields.showTags
          ? card.tags.map((tag) => (
              <span key={tag.name} data-testid={id(ids.cardTag)} data-tone={tag.colour ?? 'muted'}>
                <Chip tone={tag.colour ?? 'muted'}>{tag.name}</Chip>
              </span>
            ))
          : null}
        <span className="flex-1" />
        {fields.showPoints && (card.points !== null || fields.showEmptyFields) ? (
          <span data-testid={id(ids.cardPoints)}>{card.points ?? '–'}</span>
        ) : null}
        {pickUp ? (
          <button
            type="button"
            data-testid={id(ids.cardPickUp)}
            className="hidden h-5 items-center rounded-full border border-signal bg-panel px-[7px] text-[length:var(--text-2xs)] font-semibold whitespace-nowrap text-signal group-focus-within:inline-flex group-hover:inline-flex"
            onClick={pickUp}
          >
            Pick up
          </button>
        ) : null}
        {fields.showAssignee ? (
          <span data-testid={id(ids.cardAssignee)}>
            <Avatar {...(card.assignee ?? {})} />
          </span>
        ) : null}
      </span>
      <CardExtraFields fields={card.extraFields} />
    </article>
  );
}
