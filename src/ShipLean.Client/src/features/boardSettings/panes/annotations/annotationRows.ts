import type { BoardLevel } from '../../../../api/boards';
import type { CardAnnotationsJson } from '../../../../api/persisted';
import type { WorkItemTypeId } from '../../../../api/refs';
import { boardSettingsTestIds as ids } from '../../testIds';

export interface AnnotationRow {
  key: keyof CardAnnotationsJson;
  type: WorkItemTypeId;
  label: string;
  testId: string;
  /** What the roll-up counts, or why it has no effect on this board. */
  hint: string;
}

const rows: AnnotationRow[] = [
  {
    key: 'tasks',
    type: 'task',
    label: 'Tasks',
    testId: ids.annotationTasks,
    hint: 'done tasks out of all tasks under a user story or bug',
  },
  {
    key: 'stories',
    type: 'story',
    label: 'User stories',
    testId: ids.annotationStories,
    hint: 'done user stories out of all stories under a feature',
  },
  {
    key: 'bugs',
    type: 'bug',
    label: 'Bugs',
    testId: ids.annotationBugs,
    hint: 'done bugs out of all bugs under a feature',
  },
  {
    key: 'features',
    type: 'feature',
    label: 'Features',
    testId: ids.annotationFeatures,
    hint: 'done features out of all features under an epic or a feature',
  },
];

/** Which roll-ups a card on the board's level has children for. */
const effective: Record<BoardLevel, (keyof CardAnnotationsJson)[]> = {
  stories: ['tasks'],
  features: ['stories', 'bugs', 'features'],
  epics: ['features', 'stories', 'bugs'],
  taskboard: ['tasks'],
};

/** The `features` roll-up is listed on the Epics board and on the Features board (nested features); no other board lists it. */
export function annotationRowsFor(level: BoardLevel): AnnotationRow[] {
  return rows.filter((row) => row.key !== 'features' || level === 'epics' || level === 'features');
}

/** A roll-up with no children on this board's cards is listed but can't be turned on. */
export function hasEffect(level: BoardLevel, key: keyof CardAnnotationsJson): boolean {
  return effective[level].includes(key);
}
