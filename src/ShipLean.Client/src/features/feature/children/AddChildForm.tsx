import { useEffect, useRef, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { TypeIcon } from '../../../components/TypeIcon';
import { inputClass } from '../../workItem/form/inputClasses';
import { useInvalidateBacklog } from '../../workItem/useInvalidateBacklog';
import { createWorkItem, workItemKey } from '../../workItem/workItemApi';
import { featureTestIds as ids } from '../testIds';

export type ChildKind = 'story' | 'bug';

// The inline "+ User story" / "+ Bug" row on a phone. Enter or Add creates the item under the Feature at once (it is its own
// work item, not part of the form's Save); Esc or Cancel closes it.
export function AddChildForm({
  projectId,
  featureKey,
  featureId,
  kind,
  onClose,
}: {
  projectId: string;
  featureKey: string;
  featureId: string;
  kind: ChildKind;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const invalidateBacklog = useInvalidateBacklog();
  const [title, setTitle] = useState('');
  const input = useRef<HTMLInputElement>(null);
  useEffect(() => input.current?.focus(), []);
  const label = kind === 'bug' ? 'Bug' : 'User story';
  const add = useMutation({
    mutationFn: (value: string) =>
      createWorkItem({
        projectId,
        type: kind,
        title: value,
        parentId: featureId,
        sprintId: null,
        changes: null,
      }),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: workItemKey(projectId, featureKey) }),
        invalidateBacklog(projectId),
      ]),
  });
  const trimmed = title.trim();
  const submit = () => {
    if (trimmed === '' || add.isPending) return;
    add.mutate(trimmed, { onSuccess: onClose });
  };
  return (
    <form
      data-testid={ids.childAddForm}
      data-kind={kind}
      className="grid gap-1 border-t border-edge px-2 py-1.5"
      onSubmit={(event) => {
        event.preventDefault();
        submit();
      }}
    >
      <div className="flex items-center gap-2">
        <TypeIcon type={kind} />
        <input
          data-testid={ids.childAddInput}
          aria-label={`${label} title`}
          placeholder={`${label} title, then Enter`}
          className={`${inputClass} min-w-0 flex-1 touch:h-[var(--touch-target)]`}
          value={title}
          ref={input}
          onChange={(event) => setTitle(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Escape') {
              event.preventDefault();
              event.stopPropagation();
              onClose();
            }
          }}
        />
        <button
          type="submit"
          data-testid={ids.childAddSubmit}
          disabled={trimmed === '' || add.isPending}
          className="rounded-control bg-signal px-3 text-[length:var(--text-sm)] font-semibold text-signal-ink disabled:opacity-50 touch:h-[var(--touch-target)]"
        >
          Add
        </button>
        <button
          type="button"
          data-testid={ids.childAddCancel}
          className="rounded-control px-2 text-[length:var(--text-sm)] text-ink-muted active:bg-signal-soft touch:h-[var(--touch-target)]"
          onClick={onClose}
        >
          Cancel
        </button>
      </div>
      {add.error ? (
        <span
          data-testid={ids.childAddError}
          role="alert"
          className="text-[length:var(--text-xs)] text-alert"
        >
          {add.error.message}
        </span>
      ) : null}
    </form>
  );
}
