import { FormSlot } from '../../../slots/FormSlot';
import { LinksSlot } from '../../../slots/LinksSlot';
import type { AddLinkType } from '../../../routes';
import { project } from '../ct/WorkItemFormHarness';
import { UnsavedGuardHost } from '../form/UnsavedGuardHost';

// The form slot with the real links slot, mounted the way the page does when the address carries ?addLink=.
export function CausedByHarness({ itemKey, addLink }: { itemKey: string; addLink?: AddLinkType }) {
  return (
    <>
      <UnsavedGuardHost />
      <FormSlot
        project={project}
        itemKey={itemKey}
        links={<LinksSlot project={project} itemKey={itemKey} addLink={addLink} />}
        retro={<div />}
        tasks={<div />}
        discussion={<div />}
        side={<div />}
      />
    </>
  );
}
