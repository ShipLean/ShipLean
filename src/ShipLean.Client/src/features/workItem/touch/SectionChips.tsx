import { useQuery } from '@tanstack/react-query';
import type { WorkItemDto } from '@shiplean/core/api/workItems';
import { storiesAndBugsOf } from '../../feature/children/storiesAndBugs';
import { featureTestIds } from '../../feature/testIds';
import { retrosTestIds } from '../../retros/testIds';
import type { Draft } from '../form/draft';
import { workItemTestIds as ids } from '../testIds';
import {
  fetchAttachments,
  fetchComments,
  fetchHistory,
  workItemAttachmentsKey,
  workItemCommentsKey,
  workItemHistoryKey,
} from '../workItemApi';
import { useSectionChips, type SectionTarget } from './useSectionChips';

interface Chip extends SectionTarget {
  label: string;
  count?: string;
}

const countOf = (list: readonly unknown[] | undefined) =>
  list === undefined ? undefined : String(list.length);

// The counts the sections show, read from the same queries the sections load, so the chips cost no requests.
function useCounts(item: WorkItemDto) {
  const comments = useQuery({
    queryKey: workItemCommentsKey(item.projectId, item.key),
    queryFn: () => fetchComments(item.id),
  }).data;
  const attachments = useQuery({
    queryKey: workItemAttachmentsKey(item.projectId, item.key),
    queryFn: () => fetchAttachments(item.id),
  }).data;
  const history = useQuery({
    queryKey: workItemHistoryKey(item.projectId, item.key),
    queryFn: () => fetchHistory(item.id),
  }).data;
  return {
    comments: countOf(comments),
    attachments: countOf(attachments),
    history: countOf(history?.revisions),
  };
}

function chipsOf(item: WorkItemDto, draft: Draft, counts: ReturnType<typeof useCounts>): Chip[] {
  const tail: Chip[] = [
    { id: 'attachments', label: 'Attachments', testId: ids.attachments, count: counts.attachments },
    { id: 'history', label: 'History', testId: ids.history, count: counts.history },
  ];
  const links = String(draft.links.length);
  if (item.type.id === 'feature') {
    return [
      { id: 'goal', label: 'Goal', testId: featureTestIds.goal },
      { id: 'description', label: 'Description', testId: ids.description },
      { id: 'links', label: 'Links', testId: featureTestIds.links, count: links },
      {
        id: 'storiesAndBugs',
        label: 'Stories and bugs',
        testId: featureTestIds.storiesAndBugs,
        count: String(storiesAndBugsOf(item.children).length),
      },
      { id: 'retros', label: 'Retros', testId: retrosTestIds.workItemRetroSection },
      { id: 'log', label: 'Log', testId: featureTestIds.log, count: counts.comments },
      ...tail,
    ];
  }
  const criteria = draft.acceptanceCriteria;
  const tasks = item.children.filter((child) => child.type.id === 'task');
  return [
    { id: 'description', label: 'Description', testId: ids.description },
    {
      id: 'criteria',
      label: 'Criteria',
      testId: ids.criteria,
      count: `${criteria.filter((criterion) => criterion.done).length}/${criteria.length}`,
    },
    { id: 'repro', label: 'Repro steps', testId: ids.repro },
    {
      id: 'tasks',
      label: 'Tasks',
      testId: ids.tasks,
      count: `${tasks.filter((task) => task.state.category === 'completed').length}/${tasks.length}`,
    },
    { id: 'discussion', label: 'Discussion', testId: ids.discussion, count: counts.comments },
    { id: 'links', label: 'Links', testId: ids.links, count: links },
    ...tail,
  ];
}

/**
 * The work item's section chips at 900px and below (SHIP-714 D2, rule 12): one per section on the page, with its count,
 * the one being read highlighted; a tap scrolls to it. They scroll with the page and wrap, so nothing scrolls sideways.
 * Render it only at touch widths.
 */
export function SectionChips({ item, draft }: { item: WorkItemDto; draft: Draft }) {
  const counts = useCounts(item);
  const chips = chipsOf(item, draft, counts);
  const { present, current, scrollTo } = useSectionChips(chips, true);
  const shown = present.flatMap((id) => chips.filter((chip) => chip.id === id));
  return (
    <nav
      data-testid={ids.sectionChips}
      aria-label="Sections"
      className="flex flex-wrap gap-1.5 border-b border-edge bg-panel px-3 py-2"
    >
      {shown.map((chip) => (
        <button
          key={chip.id}
          type="button"
          data-testid={ids.sectionChip}
          data-section={chip.id}
          aria-current={chip.id === current ? 'true' : undefined}
          className="inline-flex h-[var(--touch-target)] items-center gap-1 rounded-full border border-edge bg-panel px-2.5 text-[length:var(--text-xs)] font-medium text-ink-muted aria-[current=true]:border-signal aria-[current=true]:bg-signal-soft aria-[current=true]:text-ink"
          onClick={() => scrollTo(chip.id)}
        >
          {chip.label}
          {chip.count === undefined ? null : (
            <span className="font-normal tabular-nums">{chip.count}</span>
          )}
        </button>
      ))}
    </nav>
  );
}
