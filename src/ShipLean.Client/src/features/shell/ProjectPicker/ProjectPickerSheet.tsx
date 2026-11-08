import { Sheet } from '../../../touch/Sheet';
import { shellTestIds as ids } from '../testIds';
import { PickerFooterLinks, PickerList, useProjectPicker } from './ProjectPicker';

// The project picker at 900px and below: a bottom sheet over the drawer (K5). The field is not focused until tapped, so
// the keyboard doesn't cover the list; + New project and Import from Jira sit in the footer. Picking closes the sheet,
// and the route change closes the drawer.
export function ProjectPickerSheet({
  currentKey,
  onClose,
}: {
  currentKey: string | undefined;
  onClose: () => void;
}) {
  const picker = useProjectPicker(onClose);
  return (
    <Sheet
      open
      title="Switch project"
      onClose={onClose}
      footer={
        picker.canCreate ? (
          <PickerFooterLinks
            onClose={onClose}
            className="flex min-h-(--touch-target) items-center rounded-control px-2 text-[length:var(--text-base)] no-underline hover:bg-signal-soft"
          />
        ) : undefined
      }
    >
      <div data-testid={ids.picker} className="grid gap-1">
        <input
          {...picker.searchProps}
          className="h-(--touch-target) w-full rounded-control border border-edge bg-canvas px-2"
        />
        <PickerList picker={picker} currentKey={currentKey} className="p-0" />
      </div>
    </Sheet>
  );
}
