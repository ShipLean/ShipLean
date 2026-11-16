import type { SprintDemoActionsDto } from '@shiplean/core/api/demos';
import { demosTestIds as ids } from './testIds';

/** Beside Create demo: the last delivery to the connection failed. Nothing at all when there is no warning. */
export function DemoCreateWarning({ actions }: { actions: SprintDemoActionsDto }) {
  if (actions.createWarning === null) return null;
  return (
    <p
      data-testid={ids.demoCreateWarning}
      className="m-0 text-[length:var(--text-sm)] text-caution"
    >
      {actions.createWarning}
    </p>
  );
}
