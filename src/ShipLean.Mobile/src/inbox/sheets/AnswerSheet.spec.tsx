import { inboxKeys, type InboxAskDto, type InboxItemDto } from '@shiplean/core/api/inbox';
import { fireEvent, screen, within } from '@testing-library/react-native';
import { StyleSheet } from 'react-native';
import { type Scheme } from '../../theme';
import {
  aDecisionAsk,
  aMentionAsk,
  anInbox,
  anInboxItem,
  aQuestionAsk,
  aStatusAsk,
  aUser,
  fakeInboxActions,
} from '../../test-support/inbox';
import { renderWithVoice } from '../../test-support/voiceUi';
import { micSlotId } from '../../voice/MicSlot.testIds';
import { InboxActionsContext } from '../InboxActions';
import type { AnswerSheetRequest } from '../InboxSheets';
import { answerQuickId, inboxIds } from '../testIds';
import { AnswerSheet } from './AnswerSheet';

jest.mock('expo-router', () => ({
  ...jest.requireActual('expo-router'),
  useRouter: () => ({ push: jest.fn() }),
}));

const PROJECT = 'project-1';

async function open(
  mode: AnswerSheetRequest['mode'],
  ask: InboxAskDto,
  scheme: Scheme,
  item: InboxItemDto = anInboxItem({ key: 'SHIP-924', revision: 7, asks: [ask] }),
) {
  const actions = fakeInboxActions();
  const onClose = jest.fn();
  const rendered = await renderWithVoice(
    <InboxActionsContext.Provider value={actions}>
      <AnswerSheet request={{ mode, item, ask, projectId: PROJECT }} onClose={onClose} />
    </InboxActionsContext.Provider>,
    { scheme },
  );
  return { actions, onClose, item, rendered };
}

const type = (text: string) => fireEvent.changeText(screen.getByTestId(inboxIds.answerField), text);
const apply = () => fireEvent.press(screen.getByTestId(inboxIds.answerApply));
const target = (item: InboxItemDto) => ({
  area: 'inbox',
  projectId: PROJECT,
  itemId: item.id,
  key: item.key,
});

describe.each<Scheme>(['light', 'dark'])('AnswerSheet (%s)', (scheme) => {
  const question = () =>
    aQuestionAsk({ number: 2, text: 'Which team?', options: ['Personal team', 'Wait for org'] });

  it('dispatches chooseOption for a quick answer sent unchanged', async () => {
    const { actions, onClose, item } = await open('question', question(), scheme);
    await fireEvent.press(screen.getByTestId(answerQuickId(1)));
    expect(screen.getByTestId(inboxIds.answerField).props.value).toBe('Wait for org');
    await apply();
    expect(actions.dispatched).toEqual([
      { ...target(item), kind: 'chooseOption', number: 2, optionIndex: 1, option: 'Wait for org' },
    ]);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('dispatches answer for a typed reply to a question', async () => {
    const { actions, onClose, item } = await open('question', question(), scheme);
    await type('  Use the personal team for now  ');
    await apply();
    expect(actions.dispatched).toEqual([
      { ...target(item), kind: 'answer', number: 2, body: 'Use the personal team for now' },
    ]);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('dispatches answer when a quick answer is edited', async () => {
    const { actions, item } = await open('question', question(), scheme);
    await fireEvent.press(screen.getByTestId(answerQuickId(0)));
    await type('Personal team, until the org is ready');
    await apply();
    expect(actions.dispatched).toEqual([
      {
        ...target(item),
        kind: 'answer',
        number: 2,
        body: 'Personal team, until the org is ready',
      },
    ]);
  });

  it('dispatches mentionReply with the typed body only', async () => {
    const ask = aMentionAsk({ mentionId: 'm-1' }, { label: '@2', from: aUser('Priya Shah') });
    const { actions, item } = await open('mention', ask, scheme);
    expect(screen.getByText(ask.mention!.excerpt)).toBeTruthy();
    expect(screen.getByTestId(inboxIds.answerSheet + '-title')).toHaveTextContent('Reply');
    expect(screen.getByTestId(inboxIds.answerField).props.placeholder).toBe('Type an answer');
    await type('On it, thanks');
    await apply();
    expect(actions.dispatched).toEqual([
      {
        ...target(item),
        kind: 'mentionReply',
        mentionId: 'm-1',
        label: '@2',
        body: 'On it, thanks',
      },
    ]);
  });

  it('dispatches discuss and says the decision stays open', async () => {
    const ask = aDecisionAsk({ number: 11, question: 'Which store?', recommended: 'Payments' });
    const { actions, item } = await open('discuss', ask, scheme);
    expect(screen.getByTestId(inboxIds.answerQuote)).toHaveTextContent(/Recommended: Payments/);
    expect(screen.getByTestId(inboxIds.answerWhere)).toHaveTextContent(/The decision stays open/);
    expect(screen.getByTestId(inboxIds.answerSheet + '-title')).toHaveTextContent('Discuss D11');
    await type('What about cost?');
    await apply();
    expect(actions.dispatched).toEqual([
      { ...target(item), kind: 'discuss', number: 11, body: 'What about cost?' },
    ]);
  });

  it('refuses a status with a line break or over 500 characters', async () => {
    const { actions, onClose } = await open('status', aStatusAsk(), scheme);
    await type('First line\nSecond line');
    expect(screen.getByTestId(inboxIds.answerError)).toHaveTextContent(/line breaks/);
    expect(screen.getByTestId(inboxIds.answerApply)).toBeDisabled();
    await apply();
    await type('x'.repeat(501));
    expect(screen.getByTestId(inboxIds.answerError)).toHaveTextContent(/500 characters/);
    expect(screen.getByTestId(inboxIds.answerApply)).toBeDisabled();
    await apply();
    await type(`${'x'.repeat(500)} `);
    expect(screen.queryByTestId(inboxIds.answerError)).toBeNull();
    expect(screen.getByTestId(inboxIds.answerApply)).toBeEnabled();
    expect(actions.dispatched).toEqual([]);
    expect(onClose).not.toHaveBeenCalled();
  });

  it('dispatches newStatus with the item revision', async () => {
    const ask = aStatusAsk({ statusLine: 'Card list built.' });
    const { actions, item } = await open('status', ask, scheme);
    expect(screen.getByTestId(inboxIds.answerQuote)).toHaveTextContent('Card list built.');
    await type('Checkout wired up.');
    await apply();
    expect(actions.dispatched).toEqual([
      {
        ...target(item),
        kind: 'newStatus',
        revision: 7,
        text: 'Checkout wired up.',
        from: 'Card list built.',
      },
    ]);
  });

  it('dispatches newStatus from an empty line and quotes nothing for a first status', async () => {
    const { actions, item } = await open('status', aStatusAsk({ statusLine: '' }), scheme);
    expect(screen.queryByTestId(inboxIds.answerQuote)).toBeNull();
    await type('Started.');
    await apply();
    expect(actions.dispatched).toEqual([
      { ...target(item), kind: 'newStatus', revision: 7, text: 'Started.', from: '' },
    ]);
  });

  it('puts the sheet body in a KeyboardAvoidingView that pads on iOS', async () => {
    await open('question', question(), scheme);
    // On iOS the 'padding' behaviour adds a paddingBottom (the keyboard's height, 0 while it is down); without a
    // behaviour the view has none, so the key tells the behaviours apart.
    const avoid = screen.getByTestId(inboxIds.answerSheet + '-keyboard');
    expect(StyleSheet.flatten(avoid.props.style)).toHaveProperty('paddingBottom', 0);
    expect(within(avoid).getByTestId(inboxIds.answerApply)).toBeTruthy();
  });

  it('keeps Apply off while the box is empty', async () => {
    const { actions, onClose } = await open('question', question(), scheme);
    expect(screen.getByTestId(inboxIds.answerApply)).toBeDisabled();
    await type('   ');
    expect(screen.getByTestId(inboxIds.answerApply)).toBeDisabled();
    await apply();
    expect(actions.dispatched).toEqual([]);
    expect(onClose).not.toHaveBeenCalled();
    await type('Yes');
    expect(screen.getByTestId(inboxIds.answerApply)).toBeEnabled();
  });

  it.each<[AnswerSheetRequest['mode'], () => InboxAskDto]>([
    ['question', question],
    ['mention', () => aMentionAsk()],
    ['discuss', () => aDecisionAsk()],
    ['status', () => aStatusAsk()],
  ])('never says anyone is waiting or will be notified (%s)', async (mode, ask) => {
    await open(mode, ask(), scheme);
    expect(screen.getByTestId(inboxIds.answerWhere)).toHaveTextContent(
      /Posted on SHIP-924\..*The next agent to pick it up reads it\./,
    );
    const sheet = screen.getByTestId(inboxIds.answerSheet);
    expect(within(sheet).queryByText(/waiting for you/i)).toBeNull();
    expect(within(sheet).queryByText(/notif/i)).toBeNull();
  });

  it('works from a request whose item is not in the Inbox', async () => {
    const outside = anInboxItem({ key: 'SHIP-77', revision: 4, asks: [] });
    const ask = aQuestionAsk({ number: 3, text: 'Why?', options: [] });
    const { actions, rendered } = await open('question', ask, scheme, outside);
    // The Inbox holds the same key at another revision with another id: the dispatch must still use request.item.
    const inInbox = anInboxItem({ key: 'SHIP-77', revision: 9, asks: [ask] });
    rendered.queryClient.setQueryData(
      inboxKeys.inbox(PROJECT),
      anInbox({ projectId: PROJECT, items: [inInbox] }),
    );
    expect(screen.getByTestId(inboxIds.answerWhere)).toHaveTextContent(/^Posted on SHIP-77\./);
    await type('Because');
    await apply();
    expect(actions.dispatched).toEqual([
      { ...target(outside), kind: 'answer', number: 3, body: 'Because' },
    ]);
    expect(actions.dispatched[0].itemId).not.toBe(inInbox.id);
  });

  it('places the mic in the dock slot inside the sheet', async () => {
    await open('question', question(), scheme);
    expect(
      within(screen.getByTestId(inboxIds.answerSheet)).getByTestId(micSlotId('dock')),
    ).toBeTruthy();
  });
});
