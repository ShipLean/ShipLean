import type { RunPageDto } from '@shiplean/core/api/inspection';
import { claudeRunPage } from '../../ct/inspectionMocks';

// The server filters errors-only; the stub answers it with the two failing rows of the synthetic run.
const errorRow = (id: string, name: string, startMs: number) => ({
  ...claudeRunPage.turns[0].rows[0],
  id,
  name,
  startMs,
  endMs: startMs + 2000,
  durationMs: 2000,
  isError: true,
});
export const errorsPage: RunPageDto = {
  ...claudeRunPage,
  turns: [
    {
      ...claudeRunPage.turns[0],
      rows: [errorRow('e1', 'Bash', 21000), errorRow('e2', 'Edit', 30000)],
    },
    { ...claudeRunPage.turns[1], rows: [] },
  ],
};
