import { useNavigate } from 'react-router-dom';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { SprintDto } from '@shiplean/core/api/sprints';
import { routePath } from '../../routes';
import type { SheetAction } from '../../touch/ActionSheet';
import { MoreButton } from '../../touch/MoreButton';
import { canCloseSprint } from '../sprints/end/canCloseSprint';
import { FlagIcon, ListIcon, PencilIcon, TickIcon } from './SprintMoreIcons';

/**
 * The sprint header's ⋯ at 900px and below (SHIP-724 D5): Edit goal, Edit sprint, All sprints and, while the sprint can
 * be closed, Close sprint. The inline All sprints link and Close sprint button are left out at that width. The ⋯ is the
 * bordered one (K1); each row has its icon, Edit sprint and Close sprint a hint, All sprints a ›, and a divider sits
 * before Close sprint (SHIP-1626 SPRINT-3).
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
    { id: 'edit-goal', label: 'Edit goal', icon: <FlagIcon />, onSelect: onEditGoal },
    {
      id: 'edit-sprint',
      label: 'Edit sprint',
      icon: <PencilIcon />,
      hint: 'Name, dates and goal',
      onSelect: onEditSprint,
    },
    {
      id: 'all-sprints',
      label: 'All sprints',
      icon: <ListIcon />,
      trailing: 'chevron',
      onSelect: () => void navigate(routePath('sprints', { key: project.key })),
    },
  ];
  if (canCloseSprint(sprint)) {
    // After a divider (an empty group), as the design separates it from the edits.
    actions.push({
      id: 'close-sprint',
      label: 'Close sprint',
      icon: <TickIcon />,
      hint: 'Choose where unfinished work goes',
      group: '',
      onSelect: onCloseSprint,
    });
  }
  return <MoreButton actions={actions} label={sprint.name} variant="bordered" />;
}
