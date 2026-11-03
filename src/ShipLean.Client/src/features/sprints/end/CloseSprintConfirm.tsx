import { Dialog } from '../../../components/Dialog';
import { sprintsTestIds as ids } from '../testIds';
import { EndsEarlyNote } from './EndsEarlyNote';

/** Closing a sprint with nothing unfinished: there is nothing to choose, so a short confirmation. */
export function CloseSprintConfirm({
  sprintName,
  endsEarly,
  onCancel,
  onClose,
  closing,
  error,
}: {
  sprintName: string;
  endsEarly: boolean;
  onCancel: () => void;
  onClose: () => void;
  closing: boolean;
  error: string | null;
}) {
  return (
    <Dialog
      title={`Close ${sprintName}`}
      onClose={onCancel}
      footer={
        <>
          <span className="flex-1" />
          <button
            type="button"
            data-testid={ids.endCancel}
            className="btn-ghost"
            onClick={onCancel}
          >
            Cancel
          </button>
          <button
            type="button"
            data-testid={ids.endApply}
            className="btn-signal"
            disabled={closing}
            onClick={onClose}
          >
            Close sprint
          </button>
        </>
      }
    >
      <p data-testid={ids.endConfirm} className="m-0">
        {`Nothing is unfinished in ${sprintName}. Once closed, it's done for everyone.`}
      </p>
      {endsEarly ? <EndsEarlyNote sprintName={sprintName} /> : null}
      <p
        data-testid={ids.endError}
        role="alert"
        hidden={error === null}
        className="m-0 text-[length:var(--text-sm)] text-alert"
      >
        {error}
      </p>
    </Dialog>
  );
}
