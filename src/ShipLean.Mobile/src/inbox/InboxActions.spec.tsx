import { asksKeys } from '@shiplean/core/api/asks';
import { inboxKeys } from '@shiplean/core/api/inbox';
import { act, fireEvent } from '@testing-library/react-native';
import { Pressable, Text } from 'react-native';
import type { Api, Executor, PendingState } from '../actions/types';
import { aPending, fakeRunner, type FakeRunner } from '../test-support/actions';
import { restoreFetch } from '../test-support/fakeApi';
import { aMe } from '../test-support/builders';
import { aDecisionAsk, anInbox, anInboxItem, aUser, inboxApi } from '../test-support/inbox';
import { aComment } from '../test-support/workItem';
import { renderWithRunner } from '../test-support/renderWithRunner';
import type { InboxAction } from './actions';
import { InboxActionsProvider, useInboxActions } from './InboxActions';

const at = { area: 'inbox', projectId: 'project-1', itemId: 'item-1', key: 'SHIP-907' } as const;

const changes = [
  { number: 3, alternativeIndex: 0, ownText: null, note: null },
  { number: 4, alternativeIndex: null, ownText: 'Keep it in the profile', note: null },
  { number: 5, alternativeIndex: 1, ownText: null, note: 'Cheaper' },
];

const review: InboxAction = { ...at, kind: 'review', confirm: [1, 2], changes };

const approve: InboxAction = { ...at, kind: 'approve', numbers: [10] };

// Dispatches the given action, undoes and retries by what the Inbox holds, and shows its answers.
function Probe({ action }: { action: InboxAction }) {
  const { dispatch, undo, retry, pending } = useInboxActions();
  return (
    <>
      <Pressable testID="dispatch" onPress={() => dispatch(action)} />
      <Pressable testID="undo" onPress={() => undo(pending[0]!.batchId)} />
      <Pressable testID="retry" onPress={() => retry(pending[0]!.id)} />
      <Text testID="answers">{JSON.stringify(pending)}</Text>
    </>
  );
}

async function render(action: InboxAction, runner: FakeRunner = fakeRunner()) {
  const screen = await renderWithRunner(
    <InboxActionsProvider>
      <Probe action={action} />
    </InboxActionsProvider>,
    { runner },
  );
  const answers = () =>
    JSON.parse(screen.getByTestId('answers').props.children as string) as {
      id: string;
      batchId: string;
      state: PendingState;
      labels: string[];
      error: string | null;
      retryable: boolean;
      action: InboxAction;
    }[];
  return Object.assign(screen, { runner, answers });
}

// What the runner holds for the batch the provider dispatched, part by part, in the given states.
function partsOf(runner: FakeRunner, states: PendingState[]) {
  const { batch, batchId } = runner.dispatched[0]!;
  return batch.map((action, i) =>
    aPending({ id: `p${i + 1}`, batchId, action, state: states[i]! }),
  );
}

const setPending = (runner: FakeRunner, items: ReturnType<typeof aPending>[]) =>
  act(() => {
    runner.setPending(items);
  });

beforeEach(() => {
  jest.useFakeTimers({ now: new Date('2030-01-01T09:00:00.000Z') });
});

afterEach(() => {
  jest.useRealTimers();
  restoreFetch();
});

describe('InboxActions', () => {
  it('dispatches a review as its confirm then one change each, in one held batch', async () => {
    const screen = await render(review);
    await fireEvent.press(screen.getByTestId('dispatch'));

    expect(screen.runner.dispatched).toHaveLength(1);
    expect(screen.runner.dispatched[0]!.batch).toEqual([
      { ...review, confirm: [1, 2], changes: [] },
      { ...review, confirm: [], changes: [changes[0]] },
      { ...review, confirm: [], changes: [changes[1]] },
      { ...review, confirm: [], changes: [changes[2]] },
    ]);
  });

  it('undoes through the runner by batch', async () => {
    const screen = await render(approve);
    await fireEvent.press(screen.getByTestId('dispatch'));
    await setPending(screen.runner, partsOf(screen.runner, ['undo']));

    await fireEvent.press(screen.getByTestId('undo'));

    expect(screen.runner.undone).toEqual([screen.runner.dispatched[0]!.batchId]);
    expect(screen.answers()).toEqual([]);
  });

  it('shows a partly failed review as one answer with only the unsent changes', async () => {
    const screen = await render(review);
    await fireEvent.press(screen.getByTestId('dispatch'));
    const parts = partsOf(screen.runner, ['sent', 'sent', 'failed', 'failed']);
    parts[2] = { ...parts[2]!, error: 'Nothing changed.', failure: 'server', retryable: true };
    await setPending(screen.runner, parts);

    const answers = screen.answers();
    expect(answers).toHaveLength(1);
    expect(answers[0]).toMatchObject({
      state: 'failed',
      labels: ['D4', 'D5'],
      error: 'Nothing changed.',
      retryable: true,
    });
    expect(answers[0]!.action).toEqual({ ...review, confirm: [], changes: changes.slice(1) });
  });

  it('retries the failed parts of a review in order', async () => {
    const screen = await render(review);
    await fireEvent.press(screen.getByTestId('dispatch'));
    const other = aPending({ id: 'other', batchId: 'batch-9', state: 'failed', action: approve });
    await setPending(screen.runner, [
      ...partsOf(screen.runner, ['sent', 'sent', 'failed', 'failed']),
      other,
    ]);

    await fireEvent.press(screen.getByTestId('retry'));

    expect(screen.runner.retried).toEqual(['p3', 'p4']);
  });

  it('passes the runner error and retryable flag through to the answer', async () => {
    const screen = await render(approve);
    const conflict = {
      error: 'Someone changed it.',
      failure: 'conflict',
      retryable: false,
    } as const;
    const server = { error: 'Nothing changed.', failure: 'server', retryable: true } as const;
    const answer: InboxAction = { ...at, itemId: 'item-2', kind: 'answer', number: 1, body: 'Yes' };
    await setPending(screen.runner, [
      aPending({ id: 'a', batchId: 'b1', state: 'failed', action: approve, ...conflict }),
      aPending({ id: 'b', batchId: 'b2', state: 'failed', action: answer, ...server }),
    ]);

    expect(screen.answers().map((a) => [a.id, a.labels, a.error, a.retryable])).toEqual([
      ['a', ['D10'], 'Someone changed it.', false],
      ['b', ['Q1'], 'Nothing changed.', true],
    ]);
  });

  it('invalidates Inbox and Answered when an answer is sent', async () => {
    const screen = await render(approve);
    const inboxKey = inboxKeys.inbox(at.projectId);
    const answeredKey = inboxKeys.answered(at.projectId);
    const otherKey = inboxKeys.inbox('project-2');
    for (const key of [inboxKey, answeredKey, otherKey]) screen.queryClient.setQueryData(key, {});
    await fireEvent.press(screen.getByTestId('dispatch'));

    await setPending(screen.runner, partsOf(screen.runner, ['undo']));
    expect(screen.queryClient.getQueryState(inboxKey)?.isInvalidated).toBe(false);

    await setPending(screen.runner, partsOf(screen.runner, ['sent']));
    expect(screen.queryClient.getQueryState(inboxKey)?.isInvalidated).toBe(true);
    expect(screen.queryClient.getQueryState(answeredKey)?.isInvalidated).toBe(true);
    expect(screen.queryClient.getQueryState(otherKey)?.isInvalidated).toBe(false);
  });

  it("invalidates the sent answer's work item asks", async () => {
    const screen = await render(approve);
    const mine = asksKeys.item(at.projectId, at.key);
    const other = asksKeys.item(at.projectId, 'SHIP-1');
    for (const key of [mine, other]) screen.queryClient.setQueryData(key, {});
    await fireEvent.press(screen.getByTestId('dispatch'));

    await setPending(screen.runner, partsOf(screen.runner, ['undo']));
    expect(screen.queryClient.getQueryState(mine)?.isInvalidated).toBe(false);

    await setPending(screen.runner, partsOf(screen.runner, ['sent']));
    expect(screen.queryClient.getQueryState(mine)?.isInvalidated).toBe(true);
    expect(screen.queryClient.getQueryState(other)?.isInvalidated).toBe(false);
  });

  describe('the executor it registers', () => {
    const api = {} as Api;
    const item = anInboxItem({ id: 'item-1', key: 'SHIP-907' });
    const uncertain = (action: InboxAction) =>
      aPending({ id: action.kind, state: 'uncertain', action });

    async function executor() {
      const screen = await render(approve);
      expect(screen.runner.registered).toHaveLength(1);
      return screen.runner.registered[0] as Executor<InboxAction>;
    }

    it('the executor reconciles an uncertain answer as notSent while an ask is still open', async () => {
      const inbox = anInbox({
        projectId: at.projectId,
        items: [{ ...item, asks: [aDecisionAsk({ number: 10 })] }],
      });
      const fake = inboxApi({}, { inbox });
      const reconcile = (await executor()).reconcile!;

      await expect(reconcile([uncertain(approve)], api)).resolves.toEqual(['notSent']);
      expect(fake.callsTo(`GET /api/projects/${at.projectId}/inbox`)).toHaveLength(1);
    });

    it('the executor reconciles an uncertain answer as sent when its asks are closed', async () => {
      // D10 is answered; D11 is still open on the item.
      const inbox = anInbox({
        projectId: at.projectId,
        items: [{ ...item, asks: [aDecisionAsk({ number: 11 })] }],
      });
      const fake = inboxApi({}, { inbox });
      const reconcile = (await executor()).reconcile!;

      await expect(reconcile([uncertain(approve)], api)).resolves.toEqual(['sent']);
      expect(fake.callsTo(`GET /api/projects/${at.projectId}/inbox`)).toHaveLength(1);
    });

    describe('an uncertain discuss', () => {
      const discussD11: InboxAction = { ...at, kind: 'discuss', number: 11, body: 'Why?' };
      const inbox = anInbox({
        projectId: at.projectId,
        items: [{ ...item, asks: [aDecisionAsk({ number: 11 })] }],
      });
      const pending = aPending({
        id: 'discuss',
        state: 'uncertain',
        action: discussD11,
        createdAt: '2030-01-01T09:00:00.000Z',
      });
      const commentsRoute = `GET /api/work-items/${at.itemId}/comments`;

      it("is sent when the caller's comment is on the item, checking once per item", async () => {
        const mine = aComment(at.itemId, {
          author: { ...aUser(), id: aMe().id },
          body: 'Why?',
          createdAt: '2030-01-01T09:00:03.000Z',
        });
        const fake = inboxApi({ [commentsRoute]: [mine] }, { inbox });
        const reconcile = (await executor()).reconcile!;

        await expect(reconcile([pending, { ...pending, id: 'again' }], api)).resolves.toEqual([
          'sent',
          'sent',
        ]);
        expect(fake.callsTo(commentsRoute)).toHaveLength(1);
        expect(fake.callsTo('GET /api/me')).toHaveLength(1);
      });

      it('is notSent when the comment is absent', async () => {
        inboxApi({ [commentsRoute]: [] }, { inbox });
        const reconcile = (await executor()).reconcile!;

        await expect(reconcile([pending], api)).resolves.toEqual(['notSent']);
      });
    });
  });
});
