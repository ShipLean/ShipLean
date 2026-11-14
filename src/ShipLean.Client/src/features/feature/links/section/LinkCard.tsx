import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { LinkCardDto } from '@shiplean/core/api/linkCards';
import { MoreButton } from '../../../../touch/MoreButton';
import { useTouchShell } from '../../../../touch/useTouchShell';
import { archiveLinkCard, editLinkCard, linkCardsKey } from '../../featureApi';
import { featureTestIds as ids } from '../../testIds';
import { LinkCardEditor } from './LinkCardEditor';
import { LinkCardMenu } from './LinkCardMenu';
import { LinkStateChip } from './LinkStateChip';
import { kindBadge, locationLabel, sourceLabel } from './linkSource';

// One URL card. Edit and Remove save straight away (no form revision); a failure keeps the card and says why.
export function LinkCard({
  card,
  projectId,
  itemKey,
}: {
  card: LinkCardDto;
  projectId: string;
  itemKey: string;
}) {
  const queryClient = useQueryClient();
  const { touch } = useTouchShell();
  const [editing, setEditing] = useState(false);
  const refetch = () =>
    queryClient.invalidateQueries({ queryKey: linkCardsKey(projectId, itemKey) });
  const edit = useMutation({
    mutationFn: ({ title, note }: { title: string; note: string }) =>
      editLinkCard(card.id, { title, note }),
    onSuccess: async () => {
      setEditing(false);
      await refetch();
    },
  });
  const remove = useMutation({ mutationFn: () => archiveLinkCard(card.id), onSuccess: refetch });
  const error = edit.error ?? remove.error;
  const source = sourceLabel(card);
  const startEdit = () => {
    remove.reset();
    setEditing(true);
  };
  const startRemove = () => {
    edit.reset();
    remove.mutate();
  };
  const stateChip = card.state ? (
    <span data-testid={ids.linkCardState} data-state={card.state}>
      <LinkStateChip state={card.state} />
    </span>
  ) : null;
  return (
    <li
      data-testid={ids.linkCard}
      data-kind={card.kind}
      className="grid grid-cols-[minmax(0,1fr)] gap-1 rounded-control border border-edge bg-panel p-2.5 text-[length:var(--text-sm)]"
    >
      <div className="flex items-start gap-2">
        {card.faviconUrl && card.group === 'page' ? (
          <img src={card.faviconUrl} alt="" className="mt-0.5 size-5 shrink-0" />
        ) : (
          <span className="mt-0.5 inline-flex h-5 shrink-0 items-center rounded-control bg-surface-2 px-1 text-[length:var(--text-xs)] font-semibold text-ink-muted">
            {kindBadge(card.kind)}
          </span>
        )}
        <div className="grid min-w-0 flex-1 grid-cols-[minmax(0,1fr)] gap-0.5">
          {editing ? (
            <LinkCardEditor
              card={card}
              saving={edit.isPending}
              onSave={(title, note) => edit.mutate({ title, note })}
              onCancel={() => {
                edit.reset();
                setEditing(false);
              }}
            />
          ) : (
            <>
              <div className="flex gap-1.5">
                {card.number === null ? null : (
                  <span data-testid={ids.linkCardNumber} className="text-ink-muted">
                    {card.kind.startsWith('gitlabMerge') ? '!' : '#'}
                    {card.number}
                  </span>
                )}
                <a
                  data-testid={ids.linkCardTitle}
                  href={card.url}
                  target="_blank"
                  rel="noreferrer"
                  className="min-w-0 flex-1 truncate font-medium hover:text-signal hover:underline touch:-my-1.5 touch:py-2"
                >
                  {card.title}
                </a>
                {touch ? stateChip : null}
              </div>
              <div className="truncate text-ink-muted">
                <span data-testid={ids.linkCardLocation}>{locationLabel(card)}</span>
                {source ? (
                  <>
                    {' · '}
                    <span data-testid={ids.linkCardSource}>{source}</span>
                  </>
                ) : null}
              </div>
              {card.note ? (
                <div data-testid={ids.linkCardNote} className="text-ink">
                  “{card.note}”
                </div>
              ) : null}
            </>
          )}
        </div>
        {card.imageUrl ? (
          <img
            data-testid={ids.linkCardImage}
            src={card.imageUrl}
            alt=""
            className="h-14 w-24 shrink-0 rounded-control object-cover touch:h-10 touch:w-16"
          />
        ) : null}
        {touch ? (
          <div className="-my-1 -mr-1 flex shrink-0 items-center">
            <button
              type="button"
              data-testid={ids.linkCardClose}
              aria-label={`Remove ${card.title}`}
              className="inline-grid size-[var(--touch-target)] place-items-center rounded-control text-ink-muted active:bg-signal-soft"
              onClick={startRemove}
            >
              ✕
            </button>
            <MoreButton
              label="Link actions"
              actions={[
                { id: 'edit', label: 'Edit title/note', onSelect: startEdit },
                { id: 'remove', label: 'Remove', tone: 'danger', onSelect: startRemove },
              ]}
            />
          </div>
        ) : (
          <>
            {stateChip}
            <LinkCardMenu onEdit={startEdit} onRemove={startRemove} />
          </>
        )}
      </div>
      {error ? (
        <p data-testid={ids.linkCardError} role="alert" className="m-0 text-danger">
          {error.message}
        </p>
      ) : null}
    </li>
  );
}
