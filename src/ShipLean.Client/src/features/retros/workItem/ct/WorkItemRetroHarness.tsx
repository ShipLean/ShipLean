import { project } from '../../../workItem/ct/WorkItemFormHarness';
import { FormSlot } from '../../../../slots/FormSlot';
import { WorkItemRetroSlot } from '../../../../slots/WorkItemRetroSlot';
import { UnsavedGuardHost } from '../../../workItem/form/UnsavedGuardHost';
import { workItemHarnessIds as harness } from '../../../workItem/ct/harnessIds';

// The form slot with the real retro slot in its node and markers in the others.
export function WorkItemRetroHarness({ itemKey }: { itemKey: string }) {
  return (
    <>
      <UnsavedGuardHost />
      <FormSlot
        project={project}
        itemKey={itemKey}
        links={<div data-testid={harness.links} />}
        retro={<WorkItemRetroSlot project={project} itemKey={itemKey} />}
        tasks={<div data-testid={harness.tasks} />}
        discussion={<div data-testid={harness.discussion} />}
        side={<div data-testid={harness.side} />}
      />
    </>
  );
}
