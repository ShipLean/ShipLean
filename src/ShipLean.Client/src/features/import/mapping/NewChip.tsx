import { importTestIds as ids } from '../testIds';

export function NewChip() {
  return (
    <span data-testid={ids.mappingNewChip} className="chip">
      new since last run
    </span>
  );
}
