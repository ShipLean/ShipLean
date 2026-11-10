import { configurePlatform } from '@shiplean/core/platform';
import { fireEvent } from '@testing-library/react-native';
import { useState } from 'react';
import { Pressable, Text } from 'react-native';
import { UNDO } from '../actions/undo';
import { mobilePlatform } from '../mobilePlatform';
import { screenIds } from '../screens/testIds';
import { session } from '../session/session';
import { InboxProvider } from '../shell/providers/inbox';
import { aPending, fakeRunner } from '../test-support/actions';
import { TEST_ORIGIN, TEST_SECRET, aMe } from '../test-support/builders';
import {
  aDecisionAsk,
  aQuestionAsk,
  anAnswered,
  anInbox,
  anInboxItem,
  fakeInboxActions,
  fakeInboxSheets,
} from '../test-support/inbox';
import { renderInbox } from '../test-support/renderInbox';
import { renderWithRunner } from '../test-support/renderWithRunner';
import { kvValues } from '../test-support/storage';
import type { InboxAction } from './actions';
import { AnsweredList } from './AnsweredList';
import { InboxActionsContext, InboxActionsProvider, useInboxActions } from './InboxActions';
import { InboxSheetsContext, InboxSheetsProvider, useInboxSheets } from './InboxSheets';
import { InboxRow } from './InboxRow';
import { OfflineBanner } from './OfflineBanner';
import { AnswerSheet } from './sheets/AnswerSheet';
import { ReviewSheet } from './sheets/ReviewSheet';
import { inboxQueryStoreKey } from './storageKey';
import { clearInboxStorage } from './storage';
import { inboxIds, inboxRowId } from './testIds';

const approve: InboxAction = {
  area: 'inbox',
  kind: 'approve',
  projectId: 'project-1',
  itemId: 'item-1',
  key: 'SHIP-907',
  numbers: [1, 2],
};

function Dispatch() {
  const { dispatch, pending } = useInboxActions();
  return (
    <>
      <Pressable testID="approve" onPress={() => dispatch(approve)} />
      <Text testID="pending">{JSON.stringify(pending.map((p) => [p.id, p.labels]))}</Text>
    </>
  );
}

function Opener() {
  const sheets = useInboxSheets();
  const item = anInboxItem({ asks: [aQuestionAsk()] });
  return (
    <>
      <Pressable
        testID="answer"
        onPress={() =>
          sheets.openAnswer({ mode: 'question', item, ask: item.asks[0], projectId: 'p1' })
        }
      />
      <Pressable
        testID="review"
        onPress={() => sheets.openReview(item, { scrollToConfirm: true, projectId: 'p1' })}
      />
      <Pressable testID="close" onPress={() => sheets.close()} />
    </>
  );
}

// The Inbox slot rendered again from state above it, as a parent's state change does.
function Rerenders() {
  const [count, setCount] = useState(0);
  return (
    <InboxProvider projectKey="SHIP">
      <Text testID="count">{count}</Text>
      <Pressable testID="again" onPress={() => setCount((c) => c + 1)} />
    </InboxProvider>
  );
}

describe('the Inbox seams', () => {
  it('renderInbox mounts screen-inbox', async () => {
    const inbox = anInbox();
    const screen = await renderInbox({ inbox, answered: anAnswered() });
    expect(screen.getByTestId(screenIds.inbox)).toBeTruthy();
  });

  it('dispatch puts one Inbox action, with its projectId, in undo through the runner with the tap window', async () => {
    const runner = fakeRunner();
    const screen = await renderWithRunner(
      <InboxActionsProvider>
        <Dispatch />
      </InboxActionsProvider>,
      { runner },
    );
    await fireEvent.press(screen.getByTestId('approve'));
    expect(runner.dispatched).toHaveLength(1);
    expect(runner.dispatched[0].batch).toEqual([approve]);
    expect(runner.dispatched[0].opts).toEqual({
      undo: UNDO.tap,
      origin: 'tap',
      allOrNothing: true,
    });

    // pending is the runner's Inbox pendings only, each with the labels it answers.
    runner.setPending([
      aPending({ id: 'a', action: approve, state: 'undo' }),
      aPending({ id: 'w' }),
    ]);
    expect(await screen.findByText(JSON.stringify([['a', ['D1', 'D2']]]))).toBeTruthy();
  });

  it('openAnswer shows sheet-answer and openReview shows sheet-review', async () => {
    // The sheets answer through the Inbox's actions.
    const screen = await renderWithRunner(
      <InboxActionsContext.Provider value={fakeInboxActions()}>
        <InboxSheetsProvider>
          <Opener />
        </InboxSheetsProvider>
      </InboxActionsContext.Provider>,
    );
    expect(screen.queryByTestId(inboxIds.answerSheet)).toBeNull();
    await fireEvent.press(screen.getByTestId('answer'));
    expect(screen.getByTestId(inboxIds.answerSheet)).toBeTruthy();
    expect(screen.queryByTestId(inboxIds.reviewSheet)).toBeNull();
    await fireEvent.press(screen.getByTestId('review'));
    expect(screen.getByTestId(inboxIds.reviewSheet)).toBeTruthy();
    expect(screen.queryByTestId(inboxIds.answerSheet)).toBeNull();
    await fireEvent.press(screen.getByTestId('close'));
    expect(screen.queryByTestId(inboxIds.reviewSheet)).toBeNull();
  });

  it('each stub renders its root id from its props', async () => {
    // A decision for the review sheet and a question for the answer sheet: the real sheets read the ask they are given.
    const question = aQuestionAsk();
    const story = anInboxItem({
      key: 'SHIP-924',
      type: 'story',
      underKey: 'SHIP-907',
      asks: [aDecisionAsk(), question],
    });
    const noop = () => undefined;
    // The row, the sheets and Answered all read the Inbox's actions.
    const screen = await renderWithRunner(
      <InboxActionsContext.Provider value={fakeInboxActions()}>
        <InboxSheetsContext.Provider value={fakeInboxSheets()}>
          <InboxRow
            projectId="p1"
            item={story}
            pending={[]}
            indented
            expanded={false}
            onToggleExpanded={noop}
            onOpenItem={noop}
          />
        </InboxSheetsContext.Provider>
        <AnswerSheet
          request={{ mode: 'question', item: story, ask: question, projectId: 'p1' }}
          onClose={noop}
        />
        <ReviewSheet item={story} scrollToConfirm={false} projectId="p1" onClose={noop} />
        <OfflineBanner loadedAtLabel="09:12" />
        <AnsweredList projectId="project-1" />
      </InboxActionsContext.Provider>,
    );
    expect(screen.getByTestId(inboxRowId('SHIP-924')).props['data-indented']).toBe(true);
    for (const id of [inboxIds.answerSheet, inboxIds.reviewSheet, inboxIds.answeredList]) {
      expect(screen.getByTestId(id)).toBeTruthy();
    }
  });

  it('registers the Inbox executor once', async () => {
    const runner = fakeRunner();
    const screen = await renderWithRunner(<Rerenders />, { runner });
    await fireEvent.press(screen.getByTestId('again'));
    await fireEvent.press(screen.getByTestId('again'));
    expect(screen.getByTestId('count').props.children).toBe(2);
    expect(runner.registered).toHaveLength(1);
    expect(runner.registered[0].area).toBe('inbox');
  });

  it('clearInboxStorage removes the current key and nothing else', () => {
    configurePlatform(mobilePlatform);
    session.abandon();
    session.beginSignIn(TEST_ORIGIN);
    session.setPendingSecret(TEST_SECRET);
    session.commit(aMe().email);
    const key = inboxQueryStoreKey(TEST_ORIGIN, aMe().email) ?? '';
    const other = inboxQueryStoreKey(TEST_ORIGIN, 'someone.else@example.com') ?? '';
    kvValues.set(key, '{"clientState":{}}');
    kvValues.set(other, '{"clientState":{}}');
    const before = new Map(kvValues);

    clearInboxStorage();

    expect(kvValues.has(key)).toBe(false);
    before.delete(key);
    expect(new Map(kvValues)).toEqual(before);
  });
});
