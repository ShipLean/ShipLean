import type { PromptDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../testIds';

/** What an admin can do to a prompt: Customize a built-in, Edit a custom in use, Use built-in. Members see none (AC2).
 * They sit at the right of the title row; Copy Markdown wraps under the title when the row is full. On a phone the
 * group dissolves into the title row: the primary button stays beside the title and the others start a new line,
 * left-aligned (prompts-phone.light.png). */
export function PromptActions({
  prompt,
  canEdit,
  onEdit,
  onUseBuiltIn,
  onResume,
  onCopy,
}: {
  prompt: PromptDto;
  canEdit: boolean;
  onEdit: () => void;
  onUseBuiltIn: () => void;
  onResume: (number: number) => void;
  onCopy: () => void;
}) {
  const inUse = canEdit && prompt.custom?.inUse === true;
  const resumable = prompt.status === 'usingBuiltIn' ? prompt.custom : null;
  const paused = canEdit && resumable !== null;
  const copy = (
    <button type="button" data-testid={ids.copyMarkdown} className="btn" onClick={onCopy}>
      Copy Markdown
    </button>
  );
  return (
    <div
      data-testid={ids.actions}
      className="ml-auto flex flex-wrap items-center justify-end gap-2 phone:contents"
    >
      {canEdit ? (
        <button
          type="button"
          data-testid={inUse || paused ? ids.actionEdit : ids.actionCustomize}
          className="btn-signal touch:min-h-[var(--touch-target)] phone:ml-auto"
          onClick={onEdit}
        >
          {inUse ? 'Edit' : paused ? 'Edit custom version' : 'Customize for this project'}
        </button>
      ) : null}
      <span aria-hidden="true" className="hidden phone:block phone:basis-full" />
      {canEdit && resumable !== null ? (
        <button
          type="button"
          data-testid={ids.actionResume}
          className="btn"
          onClick={() => onResume(resumable.number)}
        >
          Use custom v{resumable.number} again
        </button>
      ) : null}
      {inUse ? (
        <button
          type="button"
          data-testid={ids.actionUseBuiltIn}
          className="btn"
          onClick={onUseBuiltIn}
        >
          Use built-in
        </button>
      ) : null}
      {copy}
    </div>
  );
}
