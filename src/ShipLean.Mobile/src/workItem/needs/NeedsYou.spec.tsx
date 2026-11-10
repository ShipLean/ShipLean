import type { DecisionDto, QuestionDto } from '@shiplean/core/api/asks';
import type { WorkItemDto } from '@shiplean/core/api/workItems';
import { act, fireEvent, screen, waitFor } from '@testing-library/react-native';
import { QueryClient } from '@tanstack/react-query';
import { useEffect, useState, type ReactElement, type ReactNode } from 'react';
import { labelsOf, type InboxAction, type PendingAnswer } from '../../inbox/actions';
import { InboxActionsContext } from '../../inbox/InboxActions';
import { InboxSheetsContext } from '../../inbox/InboxSheets';
import { links } from '../../links';
import { aPending } from '../../test-support/actions';
import { aProject } from '../../test-support/builders';
import { apiError } from '../../test-support/fakeApi';
import { fakeInboxActions, fakeInboxSheets } from '../../test-support/inbox';
import { fakeVoice, renderWithVoice } from '../../test-support/voiceUi';
import {
  aDecision,
  aFeatureItem,
  anItemAsks,
  aQuestion,
  workItemApi,
} from '../../test-support/workItem';
import { themes } from '../../theme';
import * as ids from '../testIds';
import { NeedsYouSection } from './NeedsYouSection';

jest.mock('expo-router', () => ({
  ...jest.requireActual('expo-router'),
  useRouter: () => ({ push: mockPush, back: jest.fn(), canGoBack: () => false }),
}));
const mockPush = jest.fn();

const projectId = aProject().id;
const ASKS = (itemId: string) => `GET /api/work-items/${itemId}/asks`;

const d10 = (over: Partial<DecisionDto> = {}) =>
  aDecision({
    number: 10,
    label: 'D10',
    question: 'Download the models on Wi-Fi only?',
    recommended: 'Wi-Fi only, ask before mobile data',
    ...over,
  });
const q1 = (over: Partial<QuestionDto> = {}) =>
  aQuestion({
    number: 1,
    label: 'Q1',
    text: 'Ship 1.7B as it is, or try FunctionGemma first?',
    options: [],
    ...over,
  });

function pendingAnswer(
  item: WorkItemDto,
  action: Extract<InboxAction, { kind: 'approve' | 'discuss' | 'answer' }> extends infer A
    ? A extends InboxAction
      ? Omit<A, 'area' | 'projectId' | 'itemId' | 'key'>
      : never
    : never,
  state: PendingAnswer['state'],
  error: string | null = null,
): PendingAnswer {
  const full = {
    area: 'inbox',
    projectId,
    itemId: item.id,
    key: item.key,
    ...action,
  } as InboxAction;
  return {
    ...aPending({ action: full, state, error, batchId: 'batch-9', id: 'pending-9' }),
    action: full,
    labels: labelsOf(full),
  };
}

// Provides the Inbox actions, which a test can swap for others to move a pending answer on.
const control: { swap: (a: ReturnType<typeof fakeInboxActions>) => void } = {
  swap: () => undefined,
};
function Host({
  first,
  children,
}: {
  first: ReturnType<typeof fakeInboxActions>;
  children: ReactNode;
}) {
  const [actions, setActions] = useState(first);
  useEffect(() => {
    control.swap = setActions;
  }, []);
  return <InboxActionsContext.Provider value={actions}>{children}</InboxActionsContext.Provider>;
}

interface Opts {
  asks?: ReturnType<typeof anItemAsks>;
  pending?: PendingAnswer[];
  voice?: ReturnType<typeof fakeVoice>;
  scheme: 'light' | 'dark';
  api?: ReturnType<typeof workItemApi>;
}

async function open(item: WorkItemDto, { asks, pending, voice, scheme, api }: Opts) {
  const fake = api ?? workItemApi({}, { projectId, items: [item], asks: asks ? [asks] : [] });
  fake.install();
  const actions = fakeInboxActions(pending ?? []);
  const sheets = fakeInboxSheets();
  const ui: ReactElement = (
    <Host first={actions}>
      <InboxSheetsContext.Provider value={sheets}>
        <NeedsYouSection item={item} projectKey="SHIP" />
      </InboxSheetsContext.Provider>
    </Host>
  );
  const result = await renderWithVoice(ui, { scheme, voice });
  // The asks read starts once the project is known: wait for it, then for every read to land.
  await waitFor(() => expect(fake.callsTo(ASKS(item.id)).length).toBeGreaterThan(0));
  await waitFor(() => expect(result.queryClient.isFetching()).toBe(0));
  return { actions, sheets, fake, queryClient: result.queryClient as QueryClient, result };
}

describe.each(['light', 'dark'] as const)('Needs you on a Work item (%s)', (scheme) => {
  beforeEach(() => mockPush.mockClear());

  // Red when a card is missing, the order is questions first, or a card lacks its phrase or agent tag.
  it('renders the feature state: D10 and the agent question as cards with the phrase to say', async () => {
    const feature = aFeatureItem({ projectId });
    await open(feature, {
      scheme,
      asks: anItemAsks(feature.id, {
        decisions: [
          d10(),
          d10({ number: 11, label: 'D11', question: 'Where does the button sit?' }),
        ],
        questions: [q1()],
      }),
    });

    expect(await screen.findByTestId(ids.needsYouTitleId)).toHaveTextContent('Needs you');
    expect(screen.getByTestId(ids.needsAskTextId('D10'))).toHaveTextContent(
      'D10 · Download the models on Wi-Fi only?',
    );
    expect(screen.getByTestId(ids.needsAskTextId('Q1'))).toHaveTextContent(
      'Q1 · Ship 1.7B as it is, or try FunctionGemma first?',
    );
    expect(screen.getByTestId(ids.needsKindId('D10'))).toHaveTextContent('Decision to confirm');
    expect(screen.getByTestId(ids.needsKindId('Q1'))).toHaveTextContent('Needs input');
    expect(screen.getByTestId(ids.needsFromId('Q1'))).toHaveTextContent('Claude Code (claude.ai)');
    expect(screen.getByTestId(ids.needsAgentTagId('Q1'))).toHaveTextContent('Agent');
    expect(screen.getByTestId(ids.needsSayId('D10'))).toHaveTextContent('Or say “approve D10”');
    expect(screen.getByTestId(ids.needsSayId('Q1'))).toHaveTextContent('Or say “reply: …”');
    expect(screen.getByTestId(ids.needsBtnId('D10', 'approve'))).toBeOnTheScreen();
    expect(screen.getByTestId(ids.needsBtnId('D10', 'discuss'))).toBeOnTheScreen();
    expect(screen.getByTestId(ids.needsBtnId('Q1', 'reply'))).toBeOnTheScreen();
    expect(screen.getByTestId(ids.needsCardId('D10'))).toHaveStyle({
      backgroundColor: themes[scheme].colors.surface,
    });
    const order = screen.getAllByTestId(/^wi-needs-card-/).map((n) => n.props.testID as string);
    expect(order).toEqual([ids.needsCardId('D10'), ids.needsCardId('D11'), ids.needsCardId('Q1')]);
  });

  // Red when every open number is sent, or the number is wrong.
  it('dispatches approve with that one decision number', async () => {
    const feature = aFeatureItem({ projectId });
    const { actions } = await open(feature, {
      scheme,
      asks: anItemAsks(feature.id, {
        decisions: [d10(), d10({ number: 11, label: 'D11' })],
      }),
    });

    await fireEvent.press(await screen.findByTestId(ids.needsBtnId('D10', 'approve')));

    expect(actions.dispatched).toEqual([
      {
        area: 'inbox',
        projectId,
        itemId: feature.id,
        key: feature.key,
        kind: 'approve',
        numbers: [10],
      },
    ]);
  });

  // Red when Discuss opens another mode or another ask.
  it('opens the answer sheet in discuss mode for Discuss', async () => {
    const feature = aFeatureItem({ projectId });
    const { sheets } = await open(feature, {
      scheme,
      asks: anItemAsks(feature.id, { decisions: [d10(), d10({ number: 11, label: 'D11' })] }),
    });

    await fireEvent.press(await screen.findByTestId(ids.needsBtnId('D11', 'discuss')));

    expect(sheets.calls).toHaveLength(1);
    const call = sheets.calls[0]!;
    if (call.kind !== 'answer') throw new Error('expected an answer sheet');
    expect(call.request.mode).toBe('discuss');
    expect(call.request.ask.label).toBe('D11');
    expect(call.request.item.key).toBe(feature.key);
    expect(call.request.projectId).toBe(projectId);
  });

  // Red when Reply opens the discuss mode or the wrong question.
  it('opens the answer sheet in question mode for Reply', async () => {
    const feature = aFeatureItem({ projectId });
    const { sheets } = await open(feature, {
      scheme,
      asks: anItemAsks(feature.id, {
        questions: [q1(), q1({ number: 2, label: 'Q2', text: 'Another?' })],
      }),
    });

    await fireEvent.press(await screen.findByTestId(ids.needsBtnId('Q2', 'reply')));

    const call = sheets.calls[0]!;
    if (call.kind !== 'answer') throw new Error('expected an answer sheet');
    expect(call.request.mode).toBe('question');
    expect(call.request.ask.label).toBe('Q2');
    expect(call.request.ask.question?.text).toBe('Another?');
  });

  // Red when a card in the undo window still shows its buttons, or has no Undo.
  it('renders the applied state: an approved card shows Approving with Undo while pending', async () => {
    const feature = aFeatureItem({ projectId });
    const approving = pendingAnswer(feature, { kind: 'approve', numbers: [10] }, 'undo');
    const { actions } = await open(feature, {
      scheme,
      asks: anItemAsks(feature.id, { decisions: [d10()], questions: [q1()] }),
      pending: [approving],
    });

    expect(await screen.findByTestId(ids.needsPendingId('D10'))).toHaveTextContent('Approving…');
    expect(screen.queryByTestId(ids.needsBtnId('D10', 'approve'))).toBeNull();
    expect(screen.queryByTestId(ids.needsBtnId('D10', 'discuss'))).toBeNull();
    // The other card is untouched.
    expect(screen.getByTestId(ids.needsBtnId('Q1', 'reply'))).toBeOnTheScreen();
    await fireEvent.press(screen.getByTestId(ids.needsUndoId('D10')));
    expect(actions.undone).toEqual(['batch-9']);
  });

  // Red when queued or failed answers read like an approval, or a failure hides its error.
  it('shows Waiting to send when queued and the error with Retry when it did not send', async () => {
    const feature = aFeatureItem({ projectId });
    const queued = pendingAnswer(feature, { kind: 'approve', numbers: [10] }, 'queued');
    const failed = pendingAnswer(
      feature,
      { kind: 'answer', number: 1, body: 'Ship it' },
      'failed',
      'The item changed.',
    );
    const { actions } = await open(feature, {
      scheme,
      asks: anItemAsks(feature.id, { decisions: [d10()], questions: [q1()] }),
      pending: [queued, failed],
    });

    expect(await screen.findByTestId(ids.needsPendingId('D10'))).toHaveTextContent(
      'Waiting to send',
    );
    expect(screen.getByTestId(ids.needsPendingId('Q1'))).toHaveTextContent(
      "Didn't send The item changed.",
    );
    await fireEvent.press(screen.getByTestId(ids.needsRetryAnswerId('Q1')));
    expect(actions.retried).toEqual(['pending-9']);
  });

  // Red when waiting children are dropped or open another route.
  it('lists waiting children on a Feature and opens the child on tap', async () => {
    const feature = aFeatureItem({ projectId });
    await open(feature, {
      scheme,
      asks: anItemAsks(
        feature.id,
        {},
        {
          waitingChildren: [
            {
              id: 'child-1',
              key: 'SHIP-921',
              type: 'story',
              title: 'Pick the speech model',
              owner: null,
              ownerIsCreator: false,
              openQuestions: 1,
              openDecisions: 2,
            },
          ],
        },
      ),
    });

    const row = await screen.findByTestId(ids.needsChildId('SHIP-921'));
    expect(row).toHaveTextContent('SHIP-921 · 1 question · 2 decisions');
    await fireEvent.press(row);
    expect(mockPush).toHaveBeenCalledWith(links.item('SHIP', 'SHIP-921'));
  });

  // Red when a settled or empty item still shows the title or a card.
  it('renders nothing when nothing is open', async () => {
    const feature = aFeatureItem({ projectId });
    await open(feature, {
      scheme,
      asks: anItemAsks(feature.id, { decisions: [d10({ state: 'confirmed' })] }),
    });

    expect(screen.getByTestId(ids.needsYouSectionId)).toBeOnTheScreen();
    expect(screen.queryByTestId(ids.needsYouTitleId)).toBeNull();
    expect(screen.queryByTestId(ids.needsCardId('D10'))).toBeNull();
  });

  // Red when the phrases show while voice is disabled, or hide while it is on.
  it('hides the Say phrases while voice is disabled', async () => {
    const feature = aFeatureItem({ projectId });
    const asks = anItemAsks(feature.id, { decisions: [d10()], questions: [q1()] });
    await open(feature, {
      scheme,
      asks,
      voice: fakeVoice({ kind: 'disabled', reason: 'downloading', permission: 'granted' }),
    });

    expect(await screen.findByTestId(ids.needsBtnId('D10', 'approve'))).toBeOnTheScreen();
    expect(screen.queryByTestId(ids.needsSayId('D10'))).toBeNull();
    expect(screen.queryByTestId(ids.needsSayId('Q1'))).toBeNull();
  });

  // Red when copy says someone is waiting or notified.
  it('never says anyone is waiting', async () => {
    const feature = aFeatureItem({ projectId });
    await open(feature, {
      scheme,
      asks: anItemAsks(feature.id, { decisions: [d10()], questions: [q1()] }),
      pending: [pendingAnswer(feature, { kind: 'approve', numbers: [10] }, 'sent')],
    });

    expect(await screen.findByTestId(ids.needsPendingId('D10'))).toBeOnTheScreen();
    const text = JSON.stringify(screen.toJSON());
    expect(text).not.toMatch(/waiting for you|notified|notification/i);
    expect(screen.getByTestId(ids.needsPendingId('D10'))).toHaveTextContent(
      `Posted on ${feature.key}. The next agent to pick it up reads it.`,
    );
  });

  // Red when a failed read shows nothing, or Retry does not read again.
  it('shows an error line with Retry when the asks read fails', async () => {
    const feature = aFeatureItem({ projectId });
    const api = workItemApi(
      {},
      { projectId, items: [feature], asks: [anItemAsks(feature.id, { decisions: [d10()] })] },
    );
    api.failNext(ASKS(feature.id), apiError(500, 'Server error'));
    await open(feature, { scheme, api });

    expect(await screen.findByTestId(ids.needsYouErrorId)).toBeOnTheScreen();
    expect(screen.queryByTestId(ids.needsCardId('D10'))).toBeNull();
    await fireEvent.press(screen.getByTestId(ids.needsYouRetryId));
    expect(await screen.findByTestId(ids.needsCardId('D10'))).toBeOnTheScreen();
    expect(screen.queryByTestId(ids.needsYouErrorId)).toBeNull();
  });

  // Red when a cold open reads the asks before the project is known.
  it('reads nothing with an empty project id', async () => {
    const feature = aFeatureItem({ projectId });
    const api = workItemApi({}, { projectId, items: [feature], asks: [] });
    await open(feature, { scheme, api });
    for (const call of api.calls) expect(call.path).not.toContain('//');
  });

  // Red when an answer sent from here leaves the card on screen with the old asks.
  it('reads the asks again once an answer to this item has been sent', async () => {
    const feature = aFeatureItem({ projectId });
    const api = workItemApi(
      {},
      { projectId, items: [feature], asks: [anItemAsks(feature.id, { decisions: [d10()] })] },
    );
    await open(feature, { scheme, api });
    await waitFor(() => expect(api.callsTo(ASKS(feature.id))).toHaveLength(1));

    const sent = fakeInboxActions([
      pendingAnswer(feature, { kind: 'approve', numbers: [10] }, 'sent'),
    ]);
    await act(async () => control.swap(sent));

    await waitFor(() => expect(api.callsTo(ASKS(feature.id))).toHaveLength(2));
  });
});
