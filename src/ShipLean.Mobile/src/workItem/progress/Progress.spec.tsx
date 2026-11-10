import type { StateCategory } from '@shiplean/core/api/refs';
import type { CardDto, FeatureBoardViewDto } from '@shiplean/core/api/views';
import type { WorkItemDto } from '@shiplean/core/api/workItems';
import { fireEvent, screen, waitFor } from '@testing-library/react-native';
import type { ReactElement } from 'react';
import { aProject } from '../../test-support/builders';
import { apiError } from '../../test-support/fakeApi';
import { renderScreen } from '../../test-support/renderScreen';
import { aFeatureBoard, aFeatureItem, aStoryItem, workItemApi } from '../../test-support/workItem';
import { themes } from '../../theme';
import { primitiveIds } from '../../ui/testIds';
import { AcceptanceCriteria } from '../details/AcceptanceCriteria';
import { GoalDetails } from '../details/GoalDetails';
import * as ids from '../testIds';
import { CompactProgress } from './CompactProgress';
import { ProgressSection } from './ProgressSection';
import { StoryTasks } from './StoryTasks';

const projectId = aProject().id;

const boardRoute = (key: string) =>
  `GET /api/projects/${projectId}/work-items/by-key/${key}/feature-board`;

// SHIP-906's design numbers: 6 stories, 1 done, 2 in the current sprint; 7 of 15 tasks done; one task blocked.
const aFeature = (over: Partial<WorkItemDto> = {}) =>
  aFeatureItem({
    projectId,
    rollups: [{ type: 'story', open: 5, closed: 1, inCurrentSprint: 2, inOtherSprint: 0 }],
    ...over,
  });

const taskCard = (key: string, title: string, tags: string[] = [], owner: string | null = null) =>
  ({
    ...aFeatureBoard().rows[0].cells[0].tasks[0],
    id: `task-${key}`,
    key,
    title,
    tags: tags.map((name) => ({ name, colour: null })),
    assignee:
      owner === null
        ? null
        : { id: 'u', name: owner, initials: owner[0], avatarColour: '#000', photoUrl: null },
  }) as CardDto;

// A board whose one row is the story, with its tasks in the columns of the given categories.
function boardOf(
  storyId: string,
  tasks: Partial<Record<StateCategory, CardDto[]>>,
  header: Partial<FeatureBoardViewDto['header']> = {},
): FeatureBoardViewDto {
  const base = aFeatureBoard();
  return {
    ...base,
    header: { ...base.header, tasksClosed: 7, tasksTotal: 15, ...header },
    rows: [
      {
        ...base.rows[0],
        parent: { ...base.rows[0].parent, id: storyId },
        cells: base.columns.map((column) => ({
          stateId: column.state.id,
          tasks: tasks[column.state.category] ?? [],
        })),
      },
    ],
  };
}

// SHIP-921 under SHIP-906: 3 of 7 tasks done.
const aStory = (feature: WorkItemDto, over: Partial<WorkItemDto> = {}) =>
  aStoryItem({
    projectId,
    parent: { id: feature.id, key: feature.key, type: feature.type, title: feature.title },
    rollups: [{ type: 'task', open: 4, closed: 3, inCurrentSprint: 4, inOtherSprint: 0 }],
    ...over,
  });

async function open(
  ui: ReactElement,
  api: ReturnType<typeof workItemApi>,
  scheme: 'light' | 'dark',
  // The feature board route the screen reads, when it reads one: wait for the request to go out before waiting for it to land.
  reads?: string,
) {
  api.install();
  const result = await renderScreen(ui, { scheme });
  if (reads !== undefined) {
    await waitFor(() => expect(api.callsTo(reads).length).toBeGreaterThan(0));
  }
  await waitFor(() => expect(result.queryClient.isFetching()).toBe(0));
  return result;
}

describe.each(['light', 'dark'] as const)('Work item progress (%s)', (scheme) => {
  // Red when the counts are read from a backlog row, the blocked tag is ignored, or the legend or bar parts are missing.
  it('renders the feature state: the compact line with 1 blocked and the full bar with its legend', async () => {
    const feature = aFeature();
    const story = aStory(feature);
    const board = boardOf(story.id, {
      proposed: [taskCard('SHIP-930', 'Check model files against checksums', ['Blocked'], 'Sam')],
      inProgress: [taskCard('SHIP-931', 'Download the models')],
    });
    const api = workItemApi({}, { projectId, items: [feature, story], board });
    await open(
      <>
        <CompactProgress item={feature} projectId={projectId} />
        <ProgressSection item={feature} projectId={projectId} />
      </>,
      api,
      scheme,
      boardRoute(feature.key),
    );

    await waitFor(() =>
      expect(screen.getByTestId(ids.progressCountsId)).toHaveTextContent(
        '1 of 6 stories done · tasks 7/15 · 1 blocked',
      ),
    );
    expect(screen.getByTestId(ids.progressBlockedId)).toBeOnTheScreen();
    expect(screen.getAllByTestId(primitiveIds.progressBar)).toHaveLength(2);
    expect(screen.getAllByTestId(primitiveIds.progressDone)).toHaveLength(2);
    expect(screen.getAllByTestId(primitiveIds.progressCurrent)).toHaveLength(2);
    expect(screen.getByTestId(ids.progressLegendId('done'))).toHaveTextContent('Done 1');
    expect(screen.getByTestId(ids.progressLegendId('current'))).toHaveTextContent(
      'In the current sprint 2',
    );
    expect(screen.getByTestId(ids.progressLegendId('notPlanned'))).toHaveTextContent(
      'Not planned 3',
    );
    expect(screen.queryByTestId(ids.progressLegendId('other'))).toBeNull();
    expect(screen.getByTestId(ids.progressTallyId('stories'))).toHaveTextContent('Stories1/6');
    expect(screen.getByTestId(ids.progressTallyId('tasks'))).toHaveTextContent(
      'Tasks7/151 blocked',
    );
    expect(screen.getByTestId(ids.progressBlockedId)).toHaveStyle({
      color: themes[scheme].colors.danger,
    });
    // One board read serves both the line and the card.
    expect(api.callsTo(boardRoute(feature.key))).toHaveLength(1);
  });

  // Red when done is read from the board, resolved counts as to do, or another task is named.
  it('renders the story state: 3 of 7 done, 2 in progress, 1 to do, 1 blocked, with the blocked task named', async () => {
    const feature = aFeature();
    const story = aStory(feature);
    const board = boardOf(story.id, {
      proposed: [
        taskCard('SHIP-930', 'Check model files against checksums', ['Blocked'], 'Sam'),
        taskCard('SHIP-932', 'Write the licence notice'),
      ],
      inProgress: [taskCard('SHIP-931', 'Download the models', [], 'Ana')],
      resolved: [taskCard('SHIP-933', 'Pick the model per tier')],
      completed: [taskCard('SHIP-934', 'Spike on tiers')],
    });
    const api = workItemApi({}, { projectId, items: [feature, story], board });
    await open(
      <StoryTasks item={story} projectId={projectId} />,
      api,
      scheme,
      boardRoute(feature.key),
    );

    await waitFor(() =>
      expect(screen.getByTestId(ids.storyCountsId)).toHaveTextContent(
        '3 of 7 done · 2 in progress · 1 to do · 1 blocked',
      ),
    );
    expect(screen.getByTestId(ids.storyBlockedTaskId('SHIP-930'))).toHaveTextContent(
      'Check model files against checksums · Sam · blocked',
    );
    expect(screen.getAllByTestId(/^wi-story-blocked-/)).toHaveLength(1);
    expect(screen.queryByText(/Download the models/)).toBeNull();
    expect(screen.getByTestId(ids.storyBoardNoteId)).toHaveTextContent(
      'Tasks are moved on the web task board.',
    );
    expect(api.callsTo(boardRoute(feature.key))).toHaveLength(1);
  });

  // Red when a parentless Story reads a board or invents a split.
  it('counts a Story without a Feature parent from its rollups and names nothing', async () => {
    const feature = aFeature();
    const story = aStory(feature, { parent: null });
    const api = workItemApi({}, { projectId, items: [feature, story] });
    await open(<StoryTasks item={story} projectId={projectId} />, api, scheme);

    expect(screen.getByTestId(ids.storyCountsId)).toHaveTextContent(/^3 of 7 done$/);
    expect(screen.queryByTestId(/^wi-story-blocked-/)).toBeNull();
    expect(api.callsTo(boardRoute(feature.key))).toHaveLength(0);
  });

  // Red when an Epic reads the board or shows a tasks line.
  it('draws an Epic from its own rollups with no board read', async () => {
    const epic = aFeature({
      key: 'SHIP-900',
      type: { id: 'epic', name: 'Epic' },
      rollups: [{ type: 'story', open: 3, closed: 1, inCurrentSprint: 1, inOtherSprint: 1 }],
    });
    const api = workItemApi({}, { projectId, items: [epic] });
    await open(
      <>
        <CompactProgress item={epic} projectId={projectId} />
        <ProgressSection item={epic} projectId={projectId} />
      </>,
      api,
      scheme,
    );

    expect(screen.getByTestId(ids.progressCountsId)).toHaveTextContent(/^1 of 4 stories done$/);
    expect(screen.getByTestId(ids.progressLegendId('other'))).toHaveTextContent(
      'In another sprint 1',
    );
    expect(screen.getByTestId(ids.progressTallyId('stories'))).toHaveTextContent('Stories1/4');
    expect(screen.queryByTestId(ids.progressTallyId('tasks'))).toBeNull();
    expect(api.calls.filter((c) => c.path.endsWith('/feature-board'))).toHaveLength(0);
  });

  // Red when the goal is not clamped to three lines, the tap does nothing, or the description shows before the tap.
  it('shows the goal cut to three lines and expands it on tap', async () => {
    const feature = aFeature({
      goal: 'Let people work an item by speaking.',
      description: 'The description in full.',
      links: [{ id: 'l1' }, { id: 'l2' }] as WorkItemDto['links'],
    });
    await open(<GoalDetails item={feature} />, workItemApi({}, { projectId }), scheme);

    expect(screen.getByTestId(ids.goalTextId).props.numberOfLines).toBe(3);
    expect(screen.getByTestId(ids.goalTextId)).toHaveTextContent(
      'Let people work an item by speaking.',
    );
    expect(screen.getByTestId(ids.goalLinksId)).toHaveTextContent('2 links');
    expect(screen.queryByTestId(ids.goalDescriptionId)).toBeNull();

    await fireEvent.press(screen.getByTestId(ids.goalToggleId));

    expect(screen.getByTestId(ids.goalTextId).props.numberOfLines).toBeUndefined();
    expect(screen.getByTestId(ids.goalDescriptionId)).toHaveTextContent('The description in full.');
    // Read only: the one control is the expander.
    expect(screen.getAllByRole('button')).toHaveLength(1);
  });

  // Red when a done item is not ticked, an item is a control, or an empty list draws a card.
  it('shows the criteria read-only', async () => {
    const story = aStory(aFeature(), {
      acceptanceCriteria: [
        { text: 'Both models download on first run', done: true },
        { text: 'Downloads resume after the app is closed', done: false },
      ],
    });
    await open(<AcceptanceCriteria item={story} />, workItemApi({}, { projectId }), scheme);

    expect(screen.getByTestId(ids.criterionId(0))).toHaveTextContent(
      '✓Both models download on first run',
    );
    expect(screen.getByTestId(ids.criterionId(1))).toHaveTextContent(
      '○Downloads resume after the app is closed',
    );
    expect(screen.queryAllByRole('button')).toHaveLength(0);
    expect(screen.getByTestId(ids.criterionId(0)).props.accessibilityLabel).toBe(
      'Done: Both models download on first run',
    );
  });

  it('shows nothing for a Story with no criteria', async () => {
    const story = aStory(aFeature(), { acceptanceCriteria: [] });
    await open(<AcceptanceCriteria item={story} />, workItemApi({}, { projectId }), scheme);

    expect(screen.getByTestId(ids.acceptanceCriteriaId)).toBeOnTheScreen();
    expect(screen.queryByText('Acceptance criteria')).toBeNull();
  });

  // Red when the failed read still draws counts, hides the bar, or Retry does not read again.
  it("shows Progress couldn't load with Retry when the board read fails", async () => {
    const feature = aFeature();
    const board = boardOf('story', {});
    const api = workItemApi({}, { projectId, items: [feature], board });
    api.failNext(boardRoute(feature.key), apiError(404, 'not_found'));
    await open(
      <>
        <CompactProgress item={feature} projectId={projectId} />
        <ProgressSection item={feature} projectId={projectId} />
      </>,
      api,
      scheme,
      boardRoute(feature.key),
    );

    await screen.findByTestId(ids.progressErrorId);
    expect(screen.getByTestId(ids.progressErrorId)).toHaveTextContent(/^Progress couldn't load\./);
    expect(screen.queryByTestId(ids.progressCountsId)).toBeNull();
    expect(screen.queryByTestId(ids.progressTallyId('tasks'))).toBeNull();
    // The header's bar and the card's stories come from the item and still render.
    expect(screen.getAllByTestId(primitiveIds.progressBar)).toHaveLength(2);
    expect(screen.getByTestId(ids.progressTallyId('stories'))).toHaveTextContent('Stories1/6');

    await fireEvent.press(screen.getByTestId(ids.progressRetryId));

    expect(await screen.findByTestId(ids.progressTallyId('tasks'))).toHaveTextContent('Tasks7/15');
    expect(screen.queryByTestId(ids.progressErrorId)).toBeNull();
    expect(screen.getByTestId(ids.progressCountsId)).toHaveTextContent(/ · tasks 7\/15$/);
  });
});
