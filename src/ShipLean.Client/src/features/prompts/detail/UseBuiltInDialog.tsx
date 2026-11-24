import { useState } from 'react';
import type { PromptDto } from '@shiplean/core/api/prompts';
import { Dialog } from '../../../components/Dialog';
import { promptsTestIds as ids } from '../testIds';
import { useUseBuiltIn } from '../usePrompts';
import { errorText } from './update/errorText';

// The confirm before agents get the built-in again; the custom versions stay in History.
export function UseBuiltInDialog(props: {
  projectId: string;
  projectName: string;
  prompt: PromptDto;
  open: boolean;
  onClose: () => void;
}) {
  const use = useUseBuiltIn(props.projectId, props.prompt.key);
  const [error, setError] = useState<string | null>(null);
  if (!props.open) return null;
  const close = () => {
    setError(null);
    props.onClose();
  };
  const confirm = () => {
    setError(null);
    use.mutate(undefined, {
      onSuccess: close,
      onError: (e) => setError(errorText(e, 'The built-in could not be put in use.')),
    });
  };
  return (
    <Dialog
      title={`Use the built-in ${props.prompt.name}?`}
      onClose={close}
      testId={ids.useBuiltInDialog}
      footer={
        <>
          <button
            type="button"
            data-testid={ids.useBuiltInCancel}
            className="btn touch:min-h-[var(--touch-target)]"
            onClick={close}
          >
            Cancel
          </button>
          <button
            type="button"
            data-testid={ids.useBuiltInConfirm}
            className="btn-signal touch:min-h-[var(--touch-target)]"
            disabled={use.isPending}
            onClick={confirm}
          >
            Use built-in
          </button>
        </>
      }
    >
      <div className="grid gap-2">
        <p className="m-0">
          Agents get the built-in prompt from their next call. Runs already going keep what they
          were given.
        </p>
        <p className="m-0">Your versions stay in History, and you can use one again.</p>
        {error === null ? null : (
          <p role="alert" data-testid={ids.useBuiltInError} className="m-0 text-alert">
            {error}
          </p>
        )}
      </div>
    </Dialog>
  );
}
