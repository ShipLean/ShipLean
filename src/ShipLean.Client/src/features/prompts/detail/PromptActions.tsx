import type { PromptDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../testIds';

/** What an admin can do to a prompt: Customize a built-in, Edit a custom in use, Use built-in. Members see none (AC2). */
export function PromptActions({
  prompt,
  canEdit,
  onEdit,
  onUseBuiltIn,
}: {
  prompt: PromptDto;
  canEdit: boolean;
  onEdit: () => void;
  onUseBuiltIn: () => void;
}) {
  if (!canEdit) return null;
  const inUse = prompt.custom?.inUse === true;
  return (
    <div data-testid={ids.actions} className="flex flex-wrap items-center gap-2">
      <button
        type="button"
        data-testid={inUse ? ids.actionEdit : ids.actionCustomize}
        className="btn-signal"
        onClick={onEdit}
      >
        {inUse ? 'Edit' : 'Customize'}
      </button>
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
    </div>
  );
}
