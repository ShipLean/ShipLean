import type { InboxAskDto, InboxItemDto } from '@shiplean/core/api/inbox';
import { fireEvent, screen } from '@testing-library/react-native';
import { useState } from 'react';
import { themes, type Scheme } from '../theme';
import { aPending } from '../test-support/actions';
import {
  aDecisionAsk,
  aMentionAsk,
  anInboxItem,
  aQuestionAsk,
  aStatusAsk,
  fakeInboxActions,
  fakeInboxSheets,
} from '../test-support/inbox';
import { designInbox } from '../test-support/inboxDesign';
import { renderScreen } from '../test-support/renderScreen';
import { labelsOf, type InboxAction, type PendingAnswer } from './actions';
import { InboxActionsContext } from './InboxActions';
import { InboxRow } from './InboxRow';
import { InboxSheetsContext } from './InboxSheets';
import {
  inboxAgeId,
  inboxAskId,
  inboxBtnId,
  inboxKindId,
  inboxMoreId,
  inboxPendingId,
  inboxRowId,
} from './testIds';

const PROJECT = 'project-1';
const designItem = (key: string): InboxItemDto =>
  designInbox(PROJECT).items.find((i) => i.key === key)!;

type Detail<T> = T extends unknown ? Omit<T, 'area' | 'projectId' | 'itemId' | 'key'> : never;

function pendingAnswer(
  item: InboxItemDto,
  action: Detail<InboxAction>,
  state: PendingAnswer['state'],
): PendingAnswer {
  const full = {
    area: 'inbox',
    projectId: PROJECT,
    itemId: item.id,
    key: item.key,
    ...action,
  } as InboxAction;
  return { ...aPending({ action: full, state }), action: full, labels: labelsOf(full) };
}

function Row({
  item,
  open,
  pending,
  expanded = false,
}: {
  item: InboxItemDto;
  open: () => void;
  pending: PendingAnswer[];
  expanded?: boolean;
}) {
  const [on, setOn] = useState(expanded);
  return (
    <InboxRow
      projectId={PROJECT}
      item={item}
      pending={pending}
      indented={false}
      expanded={on}
      onToggleExpanded={() => setOn((v) => !v)}
      onOpenItem={open}
    />
  );
}

async function renderRow(
  item: InboxItemDto,
  opts: { pending?: PendingAnswer[]; expanded?: boolean; scheme?: Scheme } = {},
) {
  const actions = fakeInboxActions(opts.pending ?? []);
  const sheets = fakeInboxSheets();
  const open = jest.fn();
  await renderScreen(
    <InboxActionsContext.Provider value={actions}>
      <InboxSheetsContext.Provider value={sheets}>
        <Row item={item} open={open} pending={opts.pending ?? []} expanded={opts.expanded} />
      </InboxSheetsContext.Provider>
    </InboxActionsContext.Provider>,
    { scheme: opts.scheme },
  );
  return { actions, sheets, open };
}

const target = (item: InboxItemDto) =>
  ({ area: 'inbox', projectId: PROJECT, itemId: item.id, key: item.key }) as const;

const tap = (id: string) => fireEvent.press(screen.getByTestId(id));
const text = (id: string) => screen.getByTestId(id).props.children;

describe('InboxRow', () => {
  it("shows the first ask and the more line for the design's SHIP-906 row", async () => {
    const item = designItem('SHIP-906');
    await renderRow(item);
    expect(screen.getByTestId(inboxRowId('SHIP-906'))).toBeTruthy();
    expect(text(inboxKindId('SHIP-906', 'Q1'))).toBe('Needs input');
    expect(screen.getByText(item.asks[0]!.question!.text)).toBeTruthy();
    expect(screen.getByTestId(inboxAgeId('SHIP-906'))).toHaveTextContent('2h');
    expect(screen.getByTestId(inboxMoreId('SHIP-906'))).toHaveTextContent(
      '2 decisions too · D10, D11',
    );
    // The other asks stay closed until the more line is tapped.
    expect(screen.queryByTestId(inboxAskId('SHIP-906', 'D10'))).toBeNull();
    expect(screen.queryByTestId(inboxBtnId('SHIP-906', 'D10', 'approve'))).toBeNull();
  });

  it('dispatches approve with the number when Approve is tapped', async () => {
    const item = anInboxItem({ asks: [aDecisionAsk({ number: 10 })] });
    const { actions } = await renderRow(item);
    await tap(inboxBtnId(item.key, 'D10', 'approve'));
    expect(actions.dispatched).toEqual([{ ...target(item), kind: 'approve', numbers: [10] }]);
  });

  it('opens the answer sheet in discuss mode when Discuss is tapped', async () => {
    const ask = aDecisionAsk({ number: 11 });
    const item = anInboxItem({ asks: [ask] });
    const { actions, sheets } = await renderRow(item);
    await tap(inboxBtnId(item.key, 'D11', 'discuss'));
    expect(actions.dispatched).toEqual([]);
    expect(sheets.calls).toEqual([
      { kind: 'answer', request: { mode: 'discuss', item, ask, projectId: PROJECT } },
    ]);
  });

  it('opens the review sheet scrolled to Confirm when Approve all is tapped', async () => {
    const item = designItem('SHIP-907');
    const { actions, sheets } = await renderRow(item);
    expect(text(inboxKindId('SHIP-907', 'D1'))).toBe('8 decisions · D1–D8');
    await tap(inboxBtnId('SHIP-907', 'D1', 'approve-all'));
    expect(actions.dispatched).toEqual([]);
    expect(sheets.calls).toEqual([
      { kind: 'review', item, scrollToConfirm: true, projectId: PROJECT },
    ]);
    await tap(inboxBtnId('SHIP-907', 'D1', 'review'));
    expect(sheets.calls[1]).toEqual({
      kind: 'review',
      item,
      scrollToConfirm: false,
      projectId: PROJECT,
    });
    expect(screen.getByText('SHIP-906 waits on this', { exact: false })).toBeTruthy();
  });

  it('dispatches chooseOption with the option when an option is tapped', async () => {
    const item = designItem('SHIP-892');
    const { actions } = await renderRow(item);
    await tap(inboxBtnId('SHIP-892', 'Q7', 'option-1'));
    expect(actions.dispatched).toEqual([
      { ...target(item), kind: 'chooseOption', number: 7, optionIndex: 1, option: '50 MB' },
    ]);
  });

  it('opens the answer sheet in question mode when Reply is tapped', async () => {
    const item = designItem('SHIP-924');
    const { actions, sheets } = await renderRow(item);
    await tap(inboxBtnId('SHIP-924', 'Q1', 'reply'));
    expect(actions.dispatched).toEqual([]);
    expect(sheets.calls).toEqual([
      {
        kind: 'answer',
        request: { mode: 'question', item, ask: item.asks[0], projectId: PROJECT },
      },
    ]);
  });

  it('opens the answer sheet in mention mode when a mention is replied to', async () => {
    const item = designItem('SHIP-742');
    const { sheets } = await renderRow(item);
    await tap(inboxBtnId('SHIP-742', '@1', 'reply'));
    expect(sheets.calls).toEqual([
      { kind: 'answer', request: { mode: 'mention', item, ask: item.asks[0], projectId: PROJECT } },
    ]);
  });

  it('dispatches seen for that mention when Seen is tapped', async () => {
    const item = designItem('SHIP-742');
    const { actions } = await renderRow(item);
    await tap(inboxBtnId('SHIP-742', '@1', 'seen'));
    expect(actions.dispatched).toEqual([
      {
        ...target(item),
        kind: 'seen',
        mentionId: item.asks[0]!.mention!.mentionId,
        label: '@1',
      },
    ]);
  });

  it('dispatches stillTrue with the item revision', async () => {
    const item = { ...designItem('SHIP-743'), revision: 9 };
    const { actions } = await renderRow(item);
    await tap(inboxBtnId('SHIP-743', 'Status', 'still-true'));
    expect(actions.dispatched).toEqual([{ ...target(item), kind: 'stillTrue', revision: 9 }]);
  });

  it('opens the answer sheet in status mode for New status', async () => {
    const item = designItem('SHIP-743');
    const { actions, sheets } = await renderRow(item);
    await tap(inboxBtnId('SHIP-743', 'Status', 'new-status'));
    expect(actions.dispatched).toEqual([]);
    expect(sheets.calls).toEqual([
      { kind: 'answer', request: { mode: 'status', item, ask: item.asks[0], projectId: PROJECT } },
    ]);
  });

  it('opens the other asks in place from the more line', async () => {
    const item = designItem('SHIP-906');
    const { actions } = await renderRow(item);
    await tap(inboxMoreId('SHIP-906'));
    expect(screen.getByTestId(inboxAskId('SHIP-906', 'D10'))).toBeTruthy();
    expect(screen.getByTestId(inboxAskId('SHIP-906', 'D11'))).toBeTruthy();
    expect(text(inboxKindId('SHIP-906', 'D10'))).toBe('Decision · D10');
    expect(screen.getByTestId(inboxMoreId('SHIP-906'))).toHaveTextContent('Open SHIP-906');
    // Each opened ask answers on its own.
    await tap(inboxBtnId('SHIP-906', 'D11', 'approve'));
    expect(actions.dispatched).toEqual([{ ...target(item), kind: 'approve', numbers: [11] }]);
  });

  it('shows the decided line while the answer is in its undo window', async () => {
    const item = designItem('SHIP-906');
    const answer = pendingAnswer(item, { kind: 'approve', numbers: [10] }, 'undo');
    await renderRow(item, { pending: [answer], expanded: true });
    expect(screen.getByTestId(inboxPendingId('SHIP-906', 'D10'))).toHaveTextContent(
      'D10 approved · just now',
    );
    expect(screen.queryByTestId(inboxBtnId('SHIP-906', 'D10', 'approve'))).toBeNull();
    // D11 has no answer in flight, so it keeps its buttons.
    expect(screen.getByTestId(inboxBtnId('SHIP-906', 'D11', 'approve'))).toBeTruthy();
  });

  it("shows Sends when you're online for a queued answer", async () => {
    const item = designItem('SHIP-924');
    const answer = pendingAnswer(
      item,
      { kind: 'chooseOption', number: 1, optionIndex: 0, option: 'Personal team' },
      'queued',
    );
    await renderRow(item, { pending: [answer] });
    expect(screen.getByTestId(inboxPendingId('SHIP-924', 'Q1'))).toHaveTextContent(
      "Sends when you're online",
    );
    expect(screen.queryByTestId(inboxBtnId('SHIP-924', 'Q1', 'reply'))).toBeNull();
  });

  it.each(['sending', 'uncertain'] as const)(
    'shows Sending for a sending or uncertain answer (%s)',
    async (state) => {
      const item = designItem('SHIP-743');
      const answer = pendingAnswer(item, { kind: 'stillTrue', revision: 3 }, state);
      await renderRow(item, { pending: [answer] });
      expect(screen.getByTestId(inboxPendingId('SHIP-743', 'Status'))).toHaveTextContent(
        'Sending…',
      );
      expect(screen.queryByTestId(inboxBtnId('SHIP-743', 'Status', 'still-true'))).toBeNull();
    },
  );

  it('shows the buttons and the see-Answered hint again after a failed send', async () => {
    const item = designItem('SHIP-743');
    const answer = pendingAnswer(item, { kind: 'stillTrue', revision: 3 }, 'failed');
    const { actions } = await renderRow(item, { pending: [answer] });
    expect(screen.getByTestId(inboxPendingId('SHIP-743', 'Status'))).toHaveTextContent(
      "Didn't send · see Answered",
    );
    await tap(inboxBtnId('SHIP-743', 'Status', 'still-true'));
    expect(actions.dispatched).toHaveLength(1);
  });

  it('gives two mentions on one item their own buttons', async () => {
    const first: InboxAskDto = aMentionAsk({ mentionId: 'mention-1' });
    const second: InboxAskDto = aMentionAsk({ mentionId: 'mention-2' }, { label: '@2' });
    const item = anInboxItem({ asks: [first, second] });
    const { actions } = await renderRow(item, { expanded: true });
    await tap(inboxBtnId(item.key, '@2', 'seen'));
    await tap(inboxBtnId(item.key, '@1', 'seen'));
    expect(actions.dispatched).toEqual([
      { ...target(item), kind: 'seen', mentionId: 'mention-2', label: '@2' },
      { ...target(item), kind: 'seen', mentionId: 'mention-1', label: '@1' },
    ]);
  });

  it.each<[Scheme, boolean]>([
    ['light', true],
    ['dark', true],
    ['light', false],
    ['dark', false],
  ])('colours a long wait in the warn colour (%s, long wait %s)', async (scheme, longWait) => {
    const colors = themes[scheme].colors;
    await renderRow(anInboxItem({ key: 'SHIP-1', longWait, asks: [aStatusAsk()] }), { scheme });
    expect(screen.getByTestId(inboxAgeId('SHIP-1'))).toHaveStyle({
      color: longWait ? colors.warn : colors.muted,
    });
  });

  it('sizes the buttons to at least 44 and the body text to 15', async () => {
    const item = anInboxItem({
      asks: [aQuestionAsk({ number: 1, text: 'Which?', options: ['A'] })],
    });
    await renderRow(item);
    expect(screen.getByTestId(inboxBtnId(item.key, 'Q1', 'option-0'))).toHaveStyle({
      minHeight: 44,
    });
    expect(screen.getByText('Which?')).toHaveStyle({ fontSize: 15 });
  });

  it('calls onOpenItem when the row outside the buttons is tapped', async () => {
    const item = designItem('SHIP-906');
    const { open, actions } = await renderRow(item);
    await tap(inboxRowId('SHIP-906'));
    expect(open).toHaveBeenCalledTimes(1);
    // A button press stays a button press.
    await tap(inboxBtnId('SHIP-906', 'Q1', 'reply'));
    expect(open).toHaveBeenCalledTimes(1);
    expect(actions.dispatched).toEqual([]);
  });

  it('marks a Story row under its Feature as indented', async () => {
    const item = designItem('SHIP-924');
    await renderScreen(
      <InboxActionsContext.Provider value={fakeInboxActions()}>
        <InboxSheetsContext.Provider value={fakeInboxSheets()}>
          <InboxRow
            projectId={PROJECT}
            item={item}
            pending={[]}
            indented
            expanded={false}
            onToggleExpanded={() => undefined}
            onOpenItem={() => undefined}
          />
        </InboxSheetsContext.Provider>
      </InboxActionsContext.Provider>,
    );
    expect(screen.getByTestId(inboxRowId('SHIP-924')).props['data-indented']).toBe(true);
  });
});
