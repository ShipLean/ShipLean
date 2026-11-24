import { useState } from 'react';
import type { PromptDto } from '@shiplean/core/api/prompts';
import { useKeepMine } from '../usePrompts';
import { promptsTestIds as ids } from '../testIds';
import { Callout } from './Callout';

// "The built-in prompt changed": the text for everyone; Review the change and Keep mine as it is for admins (D2).
export function UpdateBanner(props: {
  projectId: string;
  prompt: PromptDto;
  canEdit: boolean;
  onApplyAndEdit: () => void;
  onReview: () => void;
}) {
  const { update, custom } = props.prompt;
  const keep = useKeepMine(props.projectId, props.prompt.key);
  const [keepError, setKeepError] = useState<string | null>(null);
  if (update === null)
    return <div data-testid={ids.updateBanner} data-can-edit={props.canEdit} hidden />;
  return (
    <div data-testid={ids.updateBanner} data-can-edit={props.canEdit}>
      <Callout tone="warn">
        <span data-testid={ids.updateBannerText}>
          <b className="font-semibold">The built-in prompt changed (the {update.changedLabel}).</b>{' '}
          Agents still get your custom v{custom?.number}, which started from the {update.baseLabel},
          so they miss the change until you take it.
        </span>
        {props.canEdit ? (
          <span className="flex flex-wrap items-center gap-2">
            <button
              type="button"
              data-testid={ids.updateReview}
              className="btn-signal touch:min-h-[var(--touch-target)]"
              onClick={props.onReview}
            >
              Review the change
            </button>
            <button
              type="button"
              data-testid={ids.updateBannerKeep}
              disabled={keep.isPending}
              className="inline-flex h-(--control-height) items-center rounded-control px-2 text-[length:var(--text-sm)] font-medium text-ink hover:bg-sunken focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-signal touch:min-h-(--touch-target)"
              onClick={() => {
                setKeepError(null);
                keep.mutateAsync().catch((error: unknown) => {
                  setKeepError(
                    error instanceof Error ? error.message : 'Your version could not be kept.',
                  );
                });
              }}
            >
              Keep mine as it is
            </button>
          </span>
        ) : null}
        {keepError === null ? null : (
          <span role="alert" data-testid={ids.updateBannerKeepError} className="text-alert">
            {keepError}
          </span>
        )}
      </Callout>
    </div>
  );
}
