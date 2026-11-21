import type { PromptDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../testIds';

// "The built-in prompt changed": the text for everyone, Review the change for admins (D2).
export function UpdateBanner(props: {
  projectId: string;
  prompt: PromptDto;
  canEdit: boolean;
  onApplyAndEdit: () => void;
  onReview: () => void;
}) {
  const { update, custom } = props.prompt;
  if (update === null)
    return <div data-testid={ids.updateBanner} data-can-edit={props.canEdit} hidden />;
  return (
    <div
      data-testid={ids.updateBanner}
      data-can-edit={props.canEdit}
      className="flex flex-wrap items-center gap-3 rounded-control border border-edge bg-sunken px-3 py-2"
    >
      <span data-testid={ids.updateBannerText} className="min-w-0 flex-1">
        The built-in prompt has changed (the {update.changedLabel}). Agents still get your custom v
        {custom?.number}, which started from the {update.baseLabel}, so they miss the change until
        you take it.
      </span>
      {props.canEdit ? (
        <button
          type="button"
          data-testid={ids.updateReview}
          className="btn-signal touch:min-h-[var(--touch-target)]"
          onClick={props.onReview}
        >
          Review the change
        </button>
      ) : null}
    </div>
  );
}
