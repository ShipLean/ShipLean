import type { RetroPointDto } from '@shiplean/core/api/retros';
import { routePath } from '../../../routes';

/** Where a quoted point lives: its work item's retro page, or for a sprint-level point the sprint's Retro tab. The note there highlights `point=`. */
export function quotedPointPath(projectKey: string, point: RetroPointDto): string {
  if (point.workItem)
    return routePath(
      'retroItem',
      { key: projectKey, itemKey: point.workItem.key },
      { point: point.id },
    );
  return routePath(
    'sprintRetro',
    { key: projectKey, sprintId: point.sprint!.id },
    { point: point.id },
  );
}
