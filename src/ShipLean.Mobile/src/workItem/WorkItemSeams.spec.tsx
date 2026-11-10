import { fireEvent, screen } from '@testing-library/react-native';
import { useState } from 'react';
import { Pressable, Text } from 'react-native';
import { UNDO } from '../actions/undo';
import { fakeRunner } from '../test-support/actions';
import { WorkItemProvider } from '../shell/providers/workItem';
import { renderWithRunner } from '../test-support/renderWithRunner';
import { aFeatureItem, aStoryItem } from '../test-support/workItem';
import type { WorkItemAction } from './actions';
import { ItemActionsRow } from './actionsRow/ItemActionsRow';
import { AcceptanceCriteria } from './details/AcceptanceCriteria';
import { GoalDetails } from './details/GoalDetails';
import { WorkItemHeader } from './header/WorkItemHeader';
import { NeedsYouSection } from './needs/NeedsYouSection';
import { CompactProgress } from './progress/CompactProgress';
import { ProgressSection } from './progress/ProgressSection';
import { StoryTasks } from './progress/StoryTasks';
import { AssignSheet } from './sheets/AssignSheet';
import { ChangeStateSheet } from './sheets/ChangeStateSheet';
import * as ids from './testIds';
import { Timeline } from './timeline/Timeline';
import { WorkItemActionsProvider, useWorkItemActions } from './WorkItemActions';

const note: WorkItemAction = {
  area: 'workItem',
  projectId: 'p1',
  kind: 'addNote',
  itemId: 'i1',
  key: 'SHIP-1',
  body: 'hi',
};

function Tapper() {
  const { dispatch } = useWorkItemActions();
  return (
    <Pressable testID="tap" onPress={() => dispatch(note)}>
      <Text>tap</Text>
    </Pressable>
  );
}

// Renders the slot again with new children, the way a parent's update does.
function Host() {
  const [n, setN] = useState(0);
  return (
    <WorkItemProvider projectKey="SHIP">
      <Pressable testID="bump" onPress={() => setN(n + 1)}>
        <Text>{n}</Text>
      </Pressable>
    </WorkItemProvider>
  );
}

describe('Work item seams', () => {
  it('each stub renders its root id from its props', async () => {
    const feature = aFeatureItem();
    const story = aStoryItem();
    const close = () => undefined;
    await renderWithRunner(
      <WorkItemActionsProvider>
        <WorkItemHeader item={feature} projectKey="SHIP" />
        <NeedsYouSection item={feature} projectKey="SHIP" />
        <CompactProgress item={feature} projectId="p" />
        <ProgressSection item={feature} projectId="p" />
        <StoryTasks item={story} projectId="p" />
        <GoalDetails item={feature} />
        <AcceptanceCriteria item={story} />
        <Timeline item={feature} />
        <ItemActionsRow item={feature} projectKey="SHIP" />
        <ChangeStateSheet item={feature} open onClose={close} />
        <AssignSheet item={feature} open onClose={close} />
      </WorkItemActionsProvider>,
    );
    for (const id of [
      ids.workItemHeaderId,
      ids.needsYouSectionId,
      ids.compactProgressId,
      ids.progressSectionId,
      ids.storyTasksId,
      ids.goalDetailsId,
      ids.acceptanceCriteriaId,
      ids.timelineId,
      ids.itemActionsRowId,
      ids.changeStateSheetId,
      ids.assignSheetId,
    ]) {
      expect(screen.getByTestId(id)).toBeTruthy();
    }
  });

  it('dispatches one tap action through the runner with the tap window', async () => {
    const runner = fakeRunner();
    await renderWithRunner(
      <WorkItemActionsProvider>
        <Tapper />
      </WorkItemActionsProvider>,
      { runner },
    );
    await fireEvent.press(screen.getByTestId('tap'));
    expect(runner.dispatched).toHaveLength(1);
    expect(runner.dispatched[0].batch).toEqual([note]);
    expect(runner.dispatched[0].opts).toEqual({
      undo: UNDO.tap,
      origin: 'tap',
      allOrNothing: true,
    });
  });

  it('registers the executor once', async () => {
    const runner = fakeRunner();
    await renderWithRunner(<Host />, { runner });
    await fireEvent.press(screen.getByTestId('bump'));
    await fireEvent.press(screen.getByTestId('bump'));
    expect(runner.registered).toHaveLength(1);
    expect(runner.registered[0].area).toBe('workItem');
  });
});
