import { useNavigate } from 'react-router-dom';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { SprintDto } from '@shiplean/core/api/sprints';
import { routePath } from '../../routes';
import type { SheetAction } from '../../touch/ActionSheet';
import { MoreButton } from '../../touch/MoreButton';
import { canCloseSprint } from '../sprints/end/canCloseSprint';

/**
 * The sprint header's ⋯ at 900px and below (SHIP-724 D5): Edit goal, Edit sprint, All sprints and, while the sprint can
 * be closed, Close sprint. The inline All sprints link and Close sprint button are left out at that width.
 */
export function SprintHeaderMore({
  project,
  sprint,
  onEditGoal,
  onEditSprint,
  onCloseSprint,
}: {
  project: ProjectDto;
  sprint: SprintDto;
  onEditGoal: () => void;
  onEditSprint: () => void;
  onCloseSprint: () => void;
}) {
  const navigate = useNavigate();
  const actions: SheetAction[] = [
    { id: 'edit-goal', label: 'Edit goal', onSelect: onEditGoal },
    { id: 'edit-sprint', label: 'Edit sprint', onSelect: onEditSprint },
    {
      id: 'all-sprints',
      label: 'All sprints',
      onSelect: () => void navigate(routePath('sprints', { key: project.key })),
    },
  ];
  if (canCloseSprint(sprint)) {
    actions.push({ id: 'close-sprint', label: 'Close sprint', onSelect: onCloseSprint });
  }
  return <MoreButton actions={actions} label={sprint.name} />;
}
