import { NewProject } from '../features/settings/newProject/NewProject';
import { pagesTestIds } from '../features/pages/testIds';

export type NewProjectSlotProps = Record<string, never>;

export function NewProjectSlot(props: NewProjectSlotProps) {
  void props;
  return (
    <div data-testid={pagesTestIds.slotNewProject}>
      <NewProject />
    </div>
  );
}
