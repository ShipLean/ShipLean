import type { AskHistoryLineDto } from '@shiplean/core/api/asks';
import type { CommentDto } from '@shiplean/core/api/comments';
import type { UserRef } from '@shiplean/core/api/refs';
import type { HistoryDto, RevisionDto, WorkItemDto } from '@shiplean/core/api/workItems';
import { act, fireEvent, screen, waitFor, within } from '@testing-library/react-native';
import { aProject } from '../../test-support/builders';
import { apiError } from '../../test-support/fakeApi';
import { renderScreen } from '../../test-support/renderScreen';
import { aComment, aFeatureItem, aStoryItem, workItemApi } from '../../test-support/workItem';
import { themes } from '../../theme';
import * as ids from '../testIds';
import { Timeline } from './Timeline';

const projectId = aProject().id;

const person = (name: string): UserRef => ({
  id: `u-${name}`,
  name,
  initials: name[0],
  avatarColour: '#5e81ac',
  photoUrl: null,
});

const feature = aFeatureItem({ projectId });
const story = aStoryItem({ projectId });

const note = (over: Partial<CommentDto>): CommentDto =>
  aComment(feature.id, {
    askTag: null,
    editedAt: null,
    editedLabel: null,
    superseded: null,
    agentName: null,
    ...over,
  });

const revision = (rev: number, over: Partial<RevisionDto>): RevisionDto => ({
  rev,
  changedBy: person('Priya Shah'),
  changedAt: '2030-01-01T10:00:00+00:00',
  changedAtLabel: '1 Jan',
  origin: 'user',
  changes: [],
  agentName: null,
  webhook: null,
  ...over,
});

const stateChange = {
  field: 'state',
  from: 'new',
  to: 'active',
  fromLabel: 'New',
  toLabel: 'Active',
};

const approved: AskHistoryLineDto = {
  kind: 'confirmed',
  badge: 'D10',
  from: null,
  to: null,
  note: null,
  by: person('Joel Dickson'),
  agentName: null,
  at: '2030-01-01T11:00:00+00:00',
  atLabel: '1 Jan, 11:00',
};

// Newest first, as the screen should show them: the agent's note, Joel's approval of D10, Priya's state change, Joel's voice
// note, an agent's owner change. Two notes, three changes.
const agentNote = note({
  id: 'n-agent',
  author: person('Joel Dickson'),
  agentName: 'Claude Code (claude.ai)',
  body: 'Spike results: right action 93%.\nNote text exact 100%.',
  createdAt: '2030-01-01T12:00:00+00:00',
  createdLabel: '2h ago',
});
const voiceNote = note({
  id: 'n-voice',
  author: person('Joel Dickson'),
  body: 'The listening sheet has to show which item it acts on.',
  createdAt: '2030-01-01T09:00:00+00:00',
  createdLabel: '5h ago',
  editedAt: '2030-01-01T09:05:00+00:00',
  editedLabel: '4h ago',
});
const history: HistoryDto = {
  revisions: [
    revision(8, {
      changedAt: '2030-01-01T10:00:00+00:00',
      changedAtLabel: '4h ago',
      changes: [stateChange],
    }),
    revision(7, {
      changedBy: person('Joel Dickson'),
      agentName: 'Claude Code (claude.ai)',
      changedAt: '2030-01-01T08:00:00+00:00',
      changedAtLabel: '6h ago',
      changes: [
        { field: 'assignee', from: null, to: 'p', fromLabel: null, toLabel: 'Priya Shah' },
        { field: 'statusLine', from: 'a', to: 'b'.repeat(90), fromLabel: null, toLabel: null },
      ],
    }),
  ],
  asks: [approved],
};

const entryOrder = () =>
  screen.getAllByTestId(/^wi-timeline-entry-/).map((node) => node.props.testID as string);

async function open(
  api: ReturnType<typeof workItemApi>,
  scheme: 'light' | 'dark',
  item: WorkItemDto = feature,
) {
  api.install();
  const result = await renderScreen(<Timeline item={item} />, { scheme });
  for (const half of ['comments', 'history']) {
    await waitFor(() =>
      expect(api.callsTo(`GET /api/work-items/${item.id}/${half}`).length).toBeGreaterThan(0),
    );
  }
  await waitFor(() => expect(result.queryClient.isFetching()).toBe(0));
  // The query cache tells its observers on a zero-delay timer: let that run before the screen is read.
  await act(async () => {
    await new Promise((resolve) => setTimeout(resolve, 0));
  });
  return result;
}

describe.each(['light', 'dark'] as const)('Work item timeline (%s)', (scheme) => {
  // Red when the list is grouped by kind, ordered oldest first, drops the agent tag or the revision's lines.
  it('renders the notes state: agent note with Agent tag, change events and notes, newest first', async () => {
    await open(
      workItemApi({}, { projectId, items: [feature], comments: [voiceNote, agentNote], history }),
      scheme,
    );
    expect(entryOrder()).toEqual([
      ids.timelineEntryId('n-agent'),
      ids.timelineEntryId('ask-confirmed-D10-2030-01-01T11:00:00+00:00'),
      ids.timelineEntryId('rev-8'),
      ids.timelineEntryId('n-voice'),
      ids.timelineEntryId('rev-7'),
    ]);
    expect(screen.getByTestId(ids.timelineWhoId('n-agent'))).toHaveTextContent(
      'Claude Code (claude.ai)',
    );
    expect(screen.getByTestId(ids.timelineAgentTagId('n-agent'))).toHaveTextContent('Agent');
    expect(screen.getByTestId(ids.timelineWhenId('n-agent'))).toHaveTextContent('2h ago');
    expect(screen.getByTestId(ids.timelineBodyId('n-agent')).props.children).toBe(agentNote.body);
    expect(screen.getByTestId(ids.timelineWhoId('n-voice'))).toHaveTextContent('Joel Dickson');
    expect(screen.queryByTestId(ids.timelineAgentTagId('n-voice'))).toBeNull();
    expect(screen.getByTestId(ids.timelineEditedId('n-voice'))).toHaveTextContent('edited 4h ago');
    expect(screen.getByTestId(ids.timelineChangeId('rev-8', 0))).toHaveTextContent(
      'State New → Active',
    );
    expect(screen.queryByTestId(ids.timelineAgentTagId('rev-8'))).toBeNull();
    expect(screen.getByTestId(ids.timelineChangeId('rev-7', 0))).toHaveTextContent(
      'Owner — → Priya Shah',
    );
    expect(screen.getByTestId(ids.timelineChangeId('rev-7', 1))).toHaveTextContent(
      'Status changed',
    );
    expect(screen.getByTestId(ids.timelineAgentTagId('rev-7'))).toHaveTextContent('Agent');
    const askId = 'ask-confirmed-D10-2030-01-01T11:00:00+00:00';
    expect(screen.getByTestId(ids.timelineChangeId(askId, 0))).toHaveTextContent('D10 approved');
    expect(screen.getByTestId(ids.timelineWhoId(askId))).toHaveTextContent('Joel Dickson');
    expect(screen.queryByTestId(ids.timelineEmptyId)).toBeNull();
  });

  // Red when the new entries are not compared with the previous list, the highlight is never cleared, or the first load is lit.
  it('renders the applied state: the new note and two change events highlighted at the top', async () => {
    let comments = [voiceNote];
    let current: HistoryDto = { revisions: [history.revisions[1]], asks: [] };
    const api = workItemApi(
      {
        [`GET /api/work-items/${feature.id}/comments`]: () => comments,
        [`GET /api/work-items/${feature.id}/history`]: () => current,
      },
      { projectId, items: [feature] },
    );
    const { queryClient } = await open(api, scheme);
    const lit = { backgroundColor: themes[scheme].colors.accentSoft };
    const plain = { backgroundColor: 'transparent' };
    expect(screen.getByTestId(ids.timelineEntryId('rev-7'))).toHaveStyle(plain);
    expect(screen.getByTestId(ids.timelineEntryId('n-voice'))).toHaveStyle(plain);

    jest.useFakeTimers({ doNotFake: ['nextTick', 'queueMicrotask', 'setImmediate'] });
    try {
      comments = [agentNote, voiceNote];
      current = { revisions: [history.revisions[0], history.revisions[1]], asks: [approved] };
      await act(async () => {
        await queryClient.invalidateQueries();
        await jest.advanceTimersByTimeAsync(0);
      });
      const askId = 'ask-confirmed-D10-2030-01-01T11:00:00+00:00';
      expect(entryOrder().slice(0, 3)).toEqual([
        ids.timelineEntryId('n-agent'),
        ids.timelineEntryId(askId),
        ids.timelineEntryId('rev-8'),
      ]);
      for (const id of ['n-agent', askId, 'rev-8']) {
        expect(screen.getByTestId(ids.timelineEntryId(id))).toHaveStyle(lit);
      }
      expect(screen.getByTestId(ids.timelineEntryId('n-voice'))).toHaveStyle(plain);
      expect(screen.getByTestId(ids.timelineEntryId('rev-7'))).toHaveStyle(plain);

      await act(async () => {
        await jest.advanceTimersByTimeAsync(2900);
      });
      expect(screen.getByTestId(ids.timelineEntryId('n-agent'))).toHaveStyle(lit);
      await act(async () => {
        await jest.advanceTimersByTimeAsync(200);
      });
      for (const id of ['n-agent', askId, 'rev-8']) {
        expect(screen.getByTestId(ids.timelineEntryId(id))).toHaveStyle(plain);
      }
    } finally {
      jest.useRealTimers();
    }
  });

  // Red when a filter keeps the other kind, the counts are not per kind, or the filter resets on a refetch.
  it('filters to Notes and to Changes', async () => {
    const { queryClient } = await open(
      workItemApi({}, { projectId, items: [feature], comments: [voiceNote, agentNote], history }),
      scheme,
    );
    const filter = screen.getByTestId(ids.timelineFilterId);
    expect(within(filter).getByText('5')).toBeTruthy();
    await fireEvent.press(within(filter).getByText('Notes'));
    expect(entryOrder()).toEqual([ids.timelineEntryId('n-agent'), ids.timelineEntryId('n-voice')]);
    await fireEvent.press(within(filter).getByText('Changes'));
    expect(entryOrder()).toEqual([
      ids.timelineEntryId('ask-confirmed-D10-2030-01-01T11:00:00+00:00'),
      ids.timelineEntryId('rev-8'),
      ids.timelineEntryId('rev-7'),
    ]);
    await act(async () => {
      await queryClient.invalidateQueries();
    });
    expect(entryOrder()).toHaveLength(3);
    await fireEvent.press(within(filter).getByText('All'));
    expect(entryOrder()).toHaveLength(5);
  });

  // Red when the Feature gets the comments text or a Story gets the log text.
  it('shows the web empty text for a Feature and for a Story', async () => {
    const empty: HistoryDto = { revisions: [], asks: [] };
    const first = await open(
      workItemApi({}, { projectId, items: [feature], comments: [], history: empty }),
      scheme,
      feature,
    );
    expect(screen.getByTestId(ids.timelineEmptyId)).toHaveTextContent('No log entries yet.');
    await first.unmount();
    await open(
      workItemApi({}, { projectId, items: [story], comments: [], history: empty }),
      scheme,
      story,
    );
    expect(screen.getByTestId(ids.timelineEmptyId)).toHaveTextContent('No comments yet.');
  });

  // Red when a failed history read blanks the notes, hides the message, or Retry does not read again.
  it('shows the history error and still lists the notes', async () => {
    let failing = true;
    const api = workItemApi(
      {
        [`GET /api/work-items/${feature.id}/history`]: () =>
          failing ? apiError(500, 'server', 'Boom.') : history,
      },
      { projectId, items: [feature], comments: [voiceNote, agentNote] },
    );
    await open(api, scheme);
    expect(
      within(screen.getByTestId(ids.timelineErrorId('history'))).getByText(
        'The history could not be loaded.',
      ),
    ).toBeTruthy();
    expect(screen.queryByTestId(ids.timelineErrorId('comments'))).toBeNull();
    expect(entryOrder()).toEqual([ids.timelineEntryId('n-agent'), ids.timelineEntryId('n-voice')]);
    expect(screen.queryByTestId(ids.timelineEmptyId)).toBeNull();
    failing = false;
    await fireEvent.press(screen.getByTestId(ids.timelineRetryId('history')));
    await waitFor(() => expect(screen.queryByTestId(ids.timelineErrorId('history'))).toBeNull());
    expect(entryOrder()).toHaveLength(5);
  });
});
