import type { ImportJobState } from '../../../api/persisted';
import type { ChipTone } from '../../../components/Chip';

/** How each state of an import job reads. */
export const jobStates: Record<ImportJobState, { tone: ChipTone; label: string }> = {
  queued: { tone: 'accent', label: 'Queued' },
  running: { tone: 'accent', label: 'Running' },
  done: { tone: 'ok', label: 'Done' },
  doneWithErrors: { tone: 'warn', label: 'Done with errors' },
  stopped: { tone: 'muted', label: 'Stopped' },
  failed: { tone: 'danger', label: 'Failed' },
};
