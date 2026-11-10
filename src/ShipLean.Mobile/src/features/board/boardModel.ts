import type { StateCategory } from '@shiplean/core/api/refs';
import type {
  FeatureBoardHeaderDto,
  FeatureBoardRowDto,
  FeatureBoardSectionDto,
  FeatureBoardViewDto,
} from '@shiplean/core/api/views';

// The pure rules of one Feature's board, ported from the web's featureBoardLayout.ts, FeatureBoardHeader.tsx and
// feature/children/storiesAndBugs.ts.

export interface Fraction {
  closed: number;
  total: number;
}

export interface BoardFigures {
  stories: Fraction;
  bugs: Fraction;
  tasks: Fraction;
  // Null when no story has points: the screen shows a dash.
  points: Fraction | null;
}

// Closed is completed or removed (storiesAndBugs.ts).
export const isClosedCategory = (category: StateCategory): boolean =>
  category === 'completed' || category === 'removed';

const isClosed = (row: FeatureBoardRowDto): boolean => isClosedCategory(row.parent.state.category);

function fraction(rows: FeatureBoardRowDto[]): Fraction {
  return { closed: rows.filter(isClosed).length, total: rows.length };
}

// Stories and bugs are counted apart from the rows by their type: the header's storiesClosed and storiesTotal count both
// together. Tasks and points come from the header.
export function boardFigures(view: FeatureBoardViewDto): BoardFigures {
  const { header, rows } = view;
  return {
    stories: fraction(rows.filter((row) => row.parent.type.id === 'story')),
    bugs: fraction(rows.filter((row) => row.parent.type.id === 'bug')),
    tasks: { closed: header.tasksClosed, total: header.tasksTotal },
    points:
      header.pointsTotal === null
        ? null
        : { closed: header.pointsClosed ?? 0, total: header.pointsTotal },
  };
}

// The web header's points percent: 0 with no points.
export function pointsPercent(header: FeatureBoardHeaderDto): number {
  if (header.pointsTotal === null || header.pointsTotal === 0) return 0;
  return Math.round(((header.pointsClosed ?? 0) / header.pointsTotal) * 100);
}

export interface TaskBar {
  done: number;
  doing: number;
  todo: number;
}

// A story's tasks by the board column their cell sits in: Closed is done, Active and Resolved are doing, New is to do.
export function taskBar(view: FeatureBoardViewDto, row: FeatureBoardRowDto): TaskBar {
  const categoryOf = new Map(view.columns.map((c) => [c.state.id, c.state.category]));
  const bar: TaskBar = { done: 0, doing: 0, todo: 0 };
  for (const cell of row.cells) {
    const category = categoryOf.get(cell.stateId);
    if (category === 'completed') bar.done += cell.tasks.length;
    else if (category === 'inProgress' || category === 'resolved') bar.doing += cell.tasks.length;
    else if (category === 'proposed') bar.todo += cell.tasks.length;
  }
  return bar;
}

export type GroupCategory = 'inProgress' | 'resolved' | 'proposed' | 'completed';

export interface RowGroup {
  category: GroupCategory;
  label: string;
  rows: FeatureBoardRowDto[];
  // Closed shows as one count line, without its rows.
  folded: boolean;
}

const GROUPS: { category: GroupCategory; label: string }[] = [
  { category: 'inProgress', label: 'Active' },
  { category: 'resolved', label: 'Resolved' },
  { category: 'proposed', label: 'New' },
  { category: 'completed', label: 'Closed' },
];

// Active, Resolved, New, Closed; a group with no rows is left out. Within a group the rows keep the server's order.
// A removed item sits with Closed.
export function groupRows(rows: FeatureBoardRowDto[]): RowGroup[] {
  return GROUPS.map(({ category, label }) => ({
    category,
    label,
    folded: category === 'completed',
    rows: rows.filter((row) => {
      const own = row.parent.state.category;
      return category === 'completed' ? isClosedCategory(own) : own === category;
    }),
  })).filter((group) => group.rows.length > 0);
}

export interface SectionLayout {
  section: FeatureBoardSectionDto;
  rows: FeatureBoardRowDto[];
  // The board's own stories: titled "Directly under".
  direct: boolean;
}

// None for a Feature with no Features under it. Otherwise one section per Feature in the tree, depth-first as the server
// sends them; the board's own section is left out when it has no stories of its own.
export function sectionLayout(view: FeatureBoardViewDto): SectionLayout[] {
  if (view.sections.length < 2) return [];
  return view.sections
    .map((section, index) => ({
      section,
      rows: view.rows.filter((row) => row.featureId === section.id),
      direct: index === 0,
    }))
    .filter((layout) => !(layout.direct && layout.rows.length === 0));
}

export const sectionTitle = ({ section, direct }: SectionLayout): string =>
  direct ? `Directly under ${section.key}` : `${section.key} ${section.title}`;
