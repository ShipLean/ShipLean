import { useState } from 'react';
import type { FeatureRegionProps } from '../../FeatureRegionProps';
import { featureTestIds as ids } from '../../testIds';
import { AddLinkForm } from './AddLinkForm';

// + Add link opens the form above the groups. The link saves straight away, so the root has no draft to touch.
export function AddLink({ item }: FeatureRegionProps) {
  const [open, setOpen] = useState(false);
  return (
    <div data-testid={ids.addLink} data-work-item-key={item.key} className="grid gap-2">
      {open ? (
        <AddLinkForm
          workItemId={item.id}
          projectId={item.projectId}
          itemKey={item.key}
          onClose={() => setOpen(false)}
        />
      ) : (
        <button
          type="button"
          data-testid={ids.addLinkOpen}
          className="h-7 w-fit rounded-control border border-edge bg-panel px-3 text-[length:var(--text-sm)] font-medium text-ink hover:bg-signal-soft focus-visible:outline-2 focus-visible:outline-signal"
          onClick={() => setOpen(true)}
        >
          + Add link
        </button>
      )}
    </div>
  );
}
