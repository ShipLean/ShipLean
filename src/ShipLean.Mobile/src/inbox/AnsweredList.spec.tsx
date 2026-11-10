import type { AnsweredNextDto } from '@shiplean/core/api/inbox';
import { act, fireEvent, screen } from '@testing-library/react-native';
import { aPending } from '../test-support/actions';
import { aProject } from '../test-support/builders';
import { apiError } from '../test-support/fakeApi';
import {
  anAnswered,
  anAnsweredRow,
  anInbox,
  aUser,
  fakeInboxActions,
  inboxApi,
} from '../test-support/inbox';
import { renderApp } from '../test-support/renderApp';
import { renderInbox } from '../test-support/renderInbox';
import type { Scheme } from '../theme/themes';
import { optionId, uiIds } from '../ui/testIds';
import { screenIds } from '../screens/testIds';
import type { PendingAnswer } from './actions';
import {
  answeredNextId,
  answeredPendingId,
  answeredPendingStatusId,
  answeredRetryId,
  answeredRowId,
  inboxIds,
} from './testIds';

const project = aProject();
const ANSWERED_ROUTE = `GET /api/projects/${project.id}/inbox/answered`;

const pendingAnswer = (
  id: string,
  state: PendingAnswer['state'],
  overrides: Partial<PendingAnswer> = {},
): PendingAnswer => ({
  ...aPending({ id, batchId: `batch-${id}`, state }),
  action: {
    area: 'inbox',
    projectId: project.id,
    itemId: `item-${id}`,
    key: 'SHIP-906',
    kind: 'answer',
    number: 1,
    text: 'Fifty megabytes',
  },
  labels: ['Q1'],
  ...overrides,
});

async function openAnswered(
  opts: Parameters<typeof renderInbox>[0] extends infer O ? Partial<O> : never,
  scheme: Scheme,
) {
  const result = await renderInbox({
    inbox: anInbox({ projectId: project.id }),
    ...opts,
    scheme,
  } as Parameters<typeof renderInbox>[0]);
  await fireEvent.press(await screen.findByTestId(optionId(inboxIds.segment, 'answered')));
  return result;
}

describe.each<Scheme>(['light', 'dark'])('AnsweredList (%s)', (scheme) => {
  it('shows server rows with each kind of next and with none', async () => {
    const at = { at: '2026-11-06T09:30:00+00:00' };
    const agent: AnsweredNextDto = {
      kind: 'agentPickedUp',
      by: null,
      agentName: 'Claude Code',
      atLabel: '2h ago',
      ...at,
    };
    const person: AnsweredNextDto = {
      kind: 'personReplied',
      by: aUser('Priya Shah'),
      agentName: null,
      atLabel: '1h ago',
      ...at,
    };
    const answered = anAnswered({
      rows: [
        anAnsweredRow({ key: 'SHIP-1', next: agent }),
        anAnsweredRow({ key: 'SHIP-2', next: person }),
        anAnsweredRow({ key: 'SHIP-3', next: null }),
      ],
    });
    await openAnswered({ answered, actions: fakeInboxActions() }, scheme);
    expect(await screen.findByTestId(answeredRowId('SHIP-1'))).toBeTruthy();
    expect(screen.getByTestId(answeredNextId('SHIP-1'))).toHaveTextContent(
      'Claude Code picked it up · 2h ago',
    );
    expect(screen.getByTestId(answeredNextId('SHIP-2'))).toHaveTextContent(
      'Priya Shah replied · 1h ago',
    );
    expect(screen.getByTestId(answeredNextId('SHIP-3'))).toHaveTextContent(
      'No agent has picked it up yet.',
    );
  });

  it('shows Sends when you are online for a queued answer', async () => {
    const actions = fakeInboxActions([pendingAnswer('p1', 'queued')]);
    await openAnswered({ answered: anAnswered({ rows: [] }), actions }, scheme);
    expect(await screen.findByTestId(answeredPendingStatusId('p1'))).toHaveTextContent(
      'Sends when you’re online',
    );
  });

  it('shows Sending for an uncertain answer', async () => {
    const actions = fakeInboxActions([pendingAnswer('p1', 'uncertain')]);
    await openAnswered({ answered: anAnswered({ rows: [] }), actions }, scheme);
    expect(await screen.findByTestId(answeredPendingStatusId('p1'))).toHaveTextContent(
      'Sending…',
    );
  });

  it('shows Retry for a retryable failure and retries it', async () => {
    const actions = fakeInboxActions([
      pendingAnswer('p1', 'failed', { error: 'The server is down.', retryable: true }),
    ]);
    await openAnswered({ answered: anAnswered({ rows: [] }), actions }, scheme);
    expect(await screen.findByTestId(answeredPendingStatusId('p1'))).toHaveTextContent(
      'The server is down.',
    );
    await fireEvent.press(screen.getByTestId(answeredRetryId('p1')));
    expect(actions.retried).toEqual(['p1']);
  });

  it('shows no Retry for a conflict', async () => {
    const actions = fakeInboxActions([
      pendingAnswer('p1', 'failed', { error: 'Someone changed it first.', retryable: false }),
    ]);
    await openAnswered({ answered: anAnswered({ rows: [] }), actions }, scheme);
    expect(await screen.findByText('Someone changed it first.')).toBeTruthy();
    expect(screen.queryByTestId(answeredRetryId('p1'))).toBeNull();
  });

  it('leaves answers in their undo window out', async () => {
    const actions = fakeInboxActions([
      pendingAnswer('p1', 'undo'),
      pendingAnswer('p2', 'sent'),
      pendingAnswer('p3', 'sending'),
    ]);
    await openAnswered({ answered: anAnswered({ rows: [] }), actions }, scheme);
    expect(await screen.findByTestId(answeredPendingId('p3'))).toBeTruthy();
    expect(screen.queryByTestId(answeredPendingId('p1'))).toBeNull();
    expect(screen.queryByTestId(answeredPendingId('p2'))).toBeNull();
  });

  it('says nothing was answered in the last 7 days when empty', async () => {
    await openAnswered({ answered: anAnswered({ rows: [] }), actions: fakeInboxActions() }, scheme);
    expect(await screen.findByText('Nothing answered in the last 7 days.')).toBeTruthy();
  });

  it('shows Retry when the first load fails', async () => {
    const api = inboxApi({}, { inbox: anInbox({ projectId: project.id }) });
    // The screen's own query fails, and the segment's first look refetches it once: both fail.
    api.failNext(ANSWERED_ROUTE, apiError(500, 'server_error'));
    api.failNext(ANSWERED_ROUTE, apiError(500, 'server_error'));
    await openAnswered({ api, actions: fakeInboxActions() }, scheme);
    expect(await screen.findByTestId(uiIds.errorState)).toBeTruthy();
    await fireEvent.press(screen.getByTestId(uiIds.errorStateAction));
    expect(await screen.findByTestId(/^answered-row-/)).toBeTruthy();
    expect(screen.queryByTestId(uiIds.errorState)).toBeNull();
    expect(api.callsTo(ANSWERED_ROUTE)).toHaveLength(3);
  });

  it('refetches on pull down', async () => {
    const api = inboxApi({}, { inbox: anInbox({ projectId: project.id }) });
    await openAnswered({ api, actions: fakeInboxActions() }, scheme);
    await screen.findByTestId(inboxIds.answeredList);
    const before = api.callsTo(ANSWERED_ROUTE).length;
    const { refreshControl } = screen.getByTestId(inboxIds.list).props;
    await act(async () => refreshControl.props.onRefresh());
    expect(api.callsTo(ANSWERED_ROUTE)).toHaveLength(before + 1);
  });

  it('opens the Work item screen when a row is tapped', async () => {
    const row = anAnsweredRow({ key: 'SHIP-743' });
    const api = inboxApi(
      {},
      { inbox: anInbox({ projectId: project.id }), answered: anAnswered({ rows: [row] }) },
    );
    await renderApp({ url: '/p/SHIP/inbox', api, scheme });
    await fireEvent.press(await screen.findByTestId(optionId(inboxIds.segment, 'answered')));
    await fireEvent.press(await screen.findByTestId(answeredRowId('SHIP-743')));
    expect(await screen.findByTestId(screenIds.workItem)).toBeOnTheScreen();
  });
});
