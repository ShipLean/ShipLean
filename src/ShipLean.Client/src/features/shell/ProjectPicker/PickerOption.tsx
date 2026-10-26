import type { ProjectSummaryDto } from '../../../api/projects';
import { ProjectKeyBadge } from '../ProjectKeyBadge';
import { shellTestIds as ids } from '../testIds';
import { Highlight } from './Highlight';

export function PickerOption({
  id,
  project,
  search,
  current,
  highlighted,
  onPick,
}: {
  id: string;
  project: ProjectSummaryDto;
  search: string;
  current: boolean;
  highlighted: boolean;
  onPick: () => void;
}) {
  return (
    <li
      id={id}
      role="option"
      aria-selected={highlighted}
      data-testid={ids.pickerOption}
      data-key={project.key}
      data-highlighted={highlighted}
      className={`flex h-[34px] cursor-pointer items-center gap-2 rounded-control px-2 hover:bg-signal-soft ${
        highlighted ? 'bg-signal-soft' : ''
      }`}
      onClick={onPick}
    >
      <ProjectKeyBadge current={current}>
        <Highlight text={project.key} search={search} />
      </ProjectKeyBadge>
      <span className="min-w-0 flex-1 truncate">
        <Highlight text={project.name} search={search} />
      </span>
      <span className="text-[length:var(--text-xs)] text-ink-muted">
        {current ? 'current' : project.memberCount}
      </span>
    </li>
  );
}
