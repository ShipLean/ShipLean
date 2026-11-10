import { fireEvent, screen } from '@testing-library/react-native';
import { ScrollView } from 'react-native';
import { type Scheme } from '../../theme';
import { designInbox } from '../../test-support/inboxDesign';
import { fakeInboxActions } from '../../test-support/inbox';
import { renderWithVoice } from '../../test-support/voiceUi';
import { InboxActionsContext } from '../InboxActions';
import {
  inboxIds,
  reviewChangeId,
  reviewChoiceId,
  reviewDecisionId,
  reviewOwnId,
  reviewWhyId,
} from '../testIds';
import { ReviewSheet } from './ReviewSheet';

jest.mock('expo-router', () => ({
  ...jest.requireActual('expo-router'),
  useRouter: () => ({ push: jest.fn() }),
}));

const PROJECT = 'project-1';

async function open(scheme: Scheme, scrollToConfirm = false) {
  const item = designInbox().items.find((i) => i.key === 'SHIP-907')!;
  const actions = fakeInboxActions();
  const onClose = jest.fn();
  await renderWithVoice(
    <InboxActionsContext.Provider value={actions}>
      <ReviewSheet
        item={item}
        scrollToConfirm={scrollToConfirm}
        projectId={PROJECT}
        onClose={onClose}
      />
    </InboxActionsContext.Provider>,
    { scheme },
  );
  return { item, actions, onClose };
}

const has = (text: string) => new RegExp(text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'));

const confirm = () => screen.getByTestId(inboxIds.reviewConfirm);

describe.each<Scheme>(['light', 'dark'])('ReviewSheet (%s)', (scheme) => {
  it('lists every open decision with its recommended answer chosen', async () => {
    const { item } = await open(scheme);
    expect(screen.getByTestId(inboxIds.reviewSheet)).toBeTruthy();
    for (let n = 1; n <= 8; n++) {
      const block = screen.getByTestId(reviewDecisionId(n));
      const decision = item.asks[n - 1]!.decision!;
      expect(block).toHaveTextContent(has(decision.question));
      expect(block).toHaveTextContent(has(decision.recommended));
      expect(block).not.toHaveTextContent(/Changed/);
    }
    expect(screen.queryByTestId(reviewDecisionId(9))).toBeNull();
    expect(screen.getByLabelText('Confirm 8')).toBeTruthy();
  });

  it('dispatches a review with the change when D4 is changed to the alternative', async () => {
    const { item, actions, onClose } = await open(scheme);
    await fireEvent.press(screen.getByTestId(reviewChangeId(4)));
    await fireEvent.press(screen.getByTestId(reviewChoiceId(4, 'alt-0')));
    await fireEvent.changeText(screen.getByTestId(reviewWhyId(4)), ' Keychain only is too strict ');
    expect(screen.getByTestId(reviewDecisionId(4))).toHaveTextContent(/Changed/);
    await fireEvent.press(confirm());
    expect(actions.dispatched).toEqual([
      {
        area: 'inbox',
        projectId: PROJECT,
        itemId: item.id,
        key: 'SHIP-907',
        kind: 'review',
        confirm: [1, 2, 3, 5, 6, 7, 8],
        changes: [
          { number: 4, alternativeIndex: 0, ownText: null, note: 'Keychain only is too strict' },
        ],
      },
    ]);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('keeps Confirm off until an own answer has text', async () => {
    const { actions } = await open(scheme);
    await fireEvent.press(screen.getByTestId(reviewChangeId(2)));
    await fireEvent.press(screen.getByTestId(reviewChoiceId(2, 'own')));
    expect(confirm()).toBeDisabled();
    await fireEvent.press(confirm());
    expect(actions.dispatched).toHaveLength(0);
    await fireEvent.changeText(screen.getByTestId(reviewOwnId(2)), '   ');
    expect(confirm()).toBeDisabled();
    await fireEvent.changeText(screen.getByTestId(reviewOwnId(2)), 'Both packages');
    expect(confirm()).toBeEnabled();
    await fireEvent.press(confirm());
    expect(actions.dispatched[0]).toMatchObject({
      confirm: [1, 3, 4, 5, 6, 7, 8],
      changes: [{ number: 2, alternativeIndex: null, ownText: 'Both packages', note: null }],
    });
  });

  it('opens scrolled to Confirm from Approve all', async () => {
    const spy = jest.spyOn(ScrollView.prototype, 'scrollToEnd');
    try {
      await open(scheme, true);
      fireEvent(screen.getByTestId(inboxIds.reviewScroll), 'contentSizeChange', 300, 900);
      expect(spy).toHaveBeenCalled();
      spy.mockClear();
    } finally {
      spy.mockRestore();
    }
  });

  it('sends nothing before Confirm', async () => {
    const { actions, onClose } = await open(scheme);
    await fireEvent.press(screen.getByTestId(reviewChangeId(4)));
    await fireEvent.press(screen.getByTestId(reviewChoiceId(4, 'alt-0')));
    await fireEvent.press(screen.getByTestId(inboxIds.reviewCancel));
    expect(actions.dispatched).toHaveLength(0);
    expect(onClose).toHaveBeenCalledTimes(1);
  });
});
