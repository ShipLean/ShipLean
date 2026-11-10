import { act, fireEvent, screen } from '@testing-library/react-native';
import { outboxKey } from '../actions/outbox';
import { scopeOf } from '../actions/scope';
import type { Pending } from '../actions/types';
import { apiError } from '../test-support/fakeApi';
import { TEST_ORIGIN, aMe, aProject } from '../test-support/builders';
import { inboxApi } from '../test-support/inbox';
import { designInbox } from '../test-support/inboxDesign';
import { renderApp } from '../test-support/renderApp';
import { kvValues } from '../test-support/storage';
import { projectRowId } from '../screens/projects/testIds';
import { tabIds } from '../screens/testIds';
import { primitiveIds } from '../ui/testIds';
import type { InboxAction } from './actions';
import {
  answerQuickId,
  inboxBtnId,
  inboxIds,
  inboxMoreId,
  inboxPendingId,
  inboxRowId,
  reviewChangeId,
  reviewChoiceId,
} from './testIds';

const project = aProject();
const inbox = designInbox(project.id);
const itemOf = (key: string) => inbox.items.find((i) => i.key === key)!;
const idOf = (key: string) => itemOf(key).id;

const confirmRoute = (key: string) => `POST /api/work-items/${idOf(key)}/decisions/confirm`;

beforeEach(() => {
  // The phone's outbox lives in the kv store, which the jest setup doesn't reset between specs.
  kvValues.clear();
  jest.useFakeTimers();
});

afterEach(() => {
  jest.useRealTimers();
});

const tap = (testID: string) => fireEvent.press(screen.getByTestId(testID));
const passSeconds = (s: number) => act(() => jest.advanceTimersByTimeAsync(s * 1000));
const badge = () => screen.getByTestId(tabIds.inboxBadge);

async function openInboxFromProjects(api: ReturnType<typeof inboxApi>) {
  await renderApp({ url: '/', api });
  await fireEvent.press(await screen.findByTestId(projectRowId('SHIP')));
  if (screen.queryByTestId(inboxRowId('SHIP-743')) === null) await tap(tabIds.inbox);
  await screen.findByTestId(inboxRowId('SHIP-743'));
}

describe('answering from the Inbox', () => {
  it('answers every kind of ask from Projects, with Undo', async () => {
    const api = inboxApi({}, { inbox });
    await openInboxFromProjects(api);
    expect(badge()).toHaveTextContent('15');

    // 2. Approve D10 on SHIP-906.
    await tap(inboxMoreId('SHIP-906'));
    await tap(inboxBtnId('SHIP-906', 'D10', 'approve'));
    expect(screen.getByTestId(inboxPendingId('SHIP-906', 'D10'))).toHaveTextContent(
      /^D10 approved/,
    );
    expect(badge()).toHaveTextContent('14');
    expect(api.callsTo(confirmRoute('SHIP-906'))).toHaveLength(0);
    await passSeconds(5);
    expect(api.callsTo(confirmRoute('SHIP-906')).map((c) => c.body)).toEqual([{ numbers: [10] }]);

    // 3. Approve D11, then Undo inside the window.
    await tap(inboxBtnId('SHIP-906', 'D11', 'approve'));
    expect(badge()).toHaveTextContent('13');
    await passSeconds(2);
    await tap(primitiveIds.toastAction);
    await passSeconds(10);
    expect(api.callsTo(confirmRoute('SHIP-906'))).toHaveLength(1);
    expect(badge()).toHaveTextContent('14');

    // 4. Reply on SHIP-924's question with a quick answer.
    await tap(inboxBtnId('SHIP-924', 'Q1', 'reply'));
    await tap(answerQuickId(0));
    await tap(inboxIds.answerApply);
    await passSeconds(5);
    const answer = api.callsTo(`POST /api/work-items/${idOf('SHIP-924')}/questions/1/answer`);
    expect(answer).toHaveLength(1);
    expect(JSON.stringify(answer[0]!.body)).toContain('Personal team');

    // 5. Review all on SHIP-907, change D4 to the alternative, confirm the rest.
    await tap(inboxBtnId('SHIP-907', 'D1', 'review'));
    await tap(reviewChangeId(4));
    await tap(reviewChoiceId(4, 'alt-0'));
    await tap(inboxIds.reviewConfirm);
    await passSeconds(5);
    const route907 = `/api/work-items/${idOf('SHIP-907')}`;
    expect(api.callsTo(confirmRoute('SHIP-907')).map((c) => c.body)).toEqual([
      { numbers: [1, 2, 3, 5, 6, 7, 8] },
    ]);
    const changes = api.callsTo(`POST ${route907}/decisions/4/change`);
    expect(changes).toHaveLength(1);
    expect(changes[0]!.body).toMatchObject({ kind: 'alternative', alternativeIndex: 0 });
    const order = api.calls
      .filter((c) => c.path.startsWith(route907))
      .map((c) => c.path.slice(route907.length));
    expect(order.indexOf('/decisions/confirm')).toBeLessThan(order.indexOf('/decisions/4/change'));

    // 6. Mark the mention Seen and the status line Still true.
    await tap(inboxBtnId('SHIP-742', '@1', 'seen'));
    await tap(inboxBtnId('SHIP-743', 'Status', 'still-true'));
    await passSeconds(5);
    const mentionId = itemOf('SHIP-742').asks[0]!.mention!.mentionId;
    expect(api.callsTo(`POST /api/mentions/${mentionId}/seen`)).toHaveLength(1);
    const status = api.callsTo(`POST /api/work-items/${idOf('SHIP-743')}/status/confirm`);
    expect(status.map((c) => c.body)).toEqual([{ revision: itemOf('SHIP-743').revision }]);
  });

  it("shows a failed answer as Didn't send and brings the buttons back", async () => {
    const api = inboxApi({}, { inbox });
    await openInboxFromProjects(api);
    api.failNext(confirmRoute('SHIP-906'), apiError(500, 'server_error'));
    await tap(inboxMoreId('SHIP-906'));
    await tap(inboxBtnId('SHIP-906', 'D10', 'approve'));
    expect(badge()).toHaveTextContent('14');
    await passSeconds(5);
    expect(screen.getByTestId(inboxPendingId('SHIP-906', 'D10'))).toHaveTextContent(
      "Didn't send · see Answered",
    );
    expect(screen.getByTestId(inboxBtnId('SHIP-906', 'D10', 'approve'))).toBeTruthy();
    expect(badge()).toHaveTextContent('15');
  });
});

describe('an Inbox answer kept in the outbox from before a restart', () => {
  // The phone was closed inside D10's 5 s undo window: the held tap is in the kv store when the app opens again.
  function seedHeldApprove() {
    const at = Date.now();
    const action: InboxAction = {
      area: 'inbox',
      kind: 'approve',
      projectId: project.id,
      itemId: idOf('SHIP-906'),
      key: 'SHIP-906',
      numbers: [10],
    };
    const held: Pending = {
      id: 'before-1',
      batchId: 'before-0',
      action,
      origin: 'tap',
      state: 'undo',
      undoUntil: new Date(at + 5000).toISOString(),
      error: null,
      failure: null,
      retryable: false,
      createdAt: new Date(at).toISOString(),
      undoOutcome: null,
      receipt: null,
    };
    const scope = scopeOf({ origin: TEST_ORIGIN, email: aMe().email }) as string;
    kvValues.set(
      outboxKey(scope),
      JSON.stringify({
        items: [held],
        batches: { 'before-0': { mode: 'hold', allOrNothing: true, live: true } },
        sentAt: {},
      }),
    );
  }

  // A restart gives it a fresh 5 s window.
  it('opens the app, offers Undo on the kept answer and sends it when its window ends', async () => {
    seedHeldApprove();
    const api = inboxApi({}, { inbox });

    await renderApp({ url: '/p/SHIP/inbox', api });
    await screen.findByTestId(inboxRowId('SHIP-906'));

    expect(screen.getByTestId(primitiveIds.toast)).toHaveTextContent(/D10 approved/);
    expect(api.callsTo(confirmRoute('SHIP-906'))).toHaveLength(0);
    await passSeconds(5);
    expect(api.callsTo(confirmRoute('SHIP-906')).map((c) => c.body)).toEqual([{ numbers: [10] }]);
  });
});
