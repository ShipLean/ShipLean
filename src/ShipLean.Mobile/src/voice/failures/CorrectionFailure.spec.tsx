import type { SearchItemDto, SearchViewDto } from '@shiplean/core/api/search';
import { platform } from '@shiplean/core/platform';
import { fireEvent, screen } from '@testing-library/react-native';
import { openBrowserAsync } from 'expo-web-browser';
import { fakeApi } from '../../test-support/fakeApi';
import { setConnected } from '../../test-support/netInfo';
import { renderScreen } from '../../test-support/renderScreen';
import { fakeVoice, FakeVoiceProvider } from '../../test-support/voiceUi';
import type { ScreenVoiceSpec, VoiceFailure } from '../contracts';
import type { VoiceSource } from '../internals';
import { heardQuoteId, heardQuoteMissId, heardQuoteTextId } from '../sheets/HeardQuote.testIds';
import { CorrectionFailure } from './CorrectionFailure';
import {
  correctionCandidateId,
  correctionClosestId,
  correctionFailureFixId,
  correctionFailureNothingChangedId,
  correctionFailureOpenWebId,
  correctionFailureWhatCanISayId,
  correctionFailureYesId,
  correctionStateId,
} from './CorrectionFailure.testIds';

const item = (key: string, title: string, projectKey: string): SearchItemDto => ({
  key,
  title,
  type: 'story',
  state: 'Active',
  stateCategory: 'inProgress',
  projectKey,
  parentPath: [],
  matchedIn: 'title',
  snippet: '',
  url: `/p/${projectKey}/items/${key}`,
  attachment: null,
});

const view = (items: SearchItemDto[]): SearchViewDto => ({
  query: '',
  semantic: false,
  projects: [],
  sprints: [],
  items,
  more: false,
});

function specWith(context: Record<string, unknown>): ScreenVoiceSpec {
  return {
    screen: 'test',
    actions: [],
    context: () => context,
    hotwords: () => [],
    toActions: () => ({
      ok: false,
      failure: { kind: 'noValidAction', detail: { action: null, reason: 'none' } },
    }),
  };
}

const HEARD = 'Set the onboarding story to Active';

async function show(
  failure: Extract<
    VoiceFailure,
    { kind: Exclude<VoiceFailure['kind'], 'tooShort' | 'nothingHeard' | 'unclear'> }
  >,
  opts: { heard?: string; source?: VoiceSource; spec?: ScreenVoiceSpec; projectKey?: string } = {},
) {
  const voice = fakeVoice();
  const onRetry = jest.fn();
  await renderScreen(
    <FakeVoiceProvider voice={voice}>
      <CorrectionFailure
        failure={failure}
        heard={opts.heard ?? HEARD}
        source={opts.source ?? 'voice'}
        spec={opts.spec ?? null}
        projectKey={opts.projectKey ?? 'SHIP'}
        onRetry={onRetry}
        onWhatCanISay={jest.fn()}
        onClose={jest.fn()}
      />
    </FakeVoiceProvider>,
  );
  return { voice, onRetry };
}

const noMatch: Parameters<typeof show>[0] = {
  kind: 'noMatch',
  detail: { spoken: 'the onboarding story', role: 'item', span: null },
};

beforeEach(() => {
  setConnected(true);
  jest.mocked(openBrowserAsync).mockClear();
});

describe('CorrectionFailure', () => {
  it("noMatch shows search's two closest in this project, and a tap retries with that key", async () => {
    const api = fakeApi({
      'GET /api/search': view([
        item('PAY-9', 'Onboarding for payments', 'PAY'),
        item('SHIP-5', 'Onboarding story', 'SHIP'),
        item('SHIP-6', 'Onboarding copy', 'SHIP'),
        item('SHIP-7', 'Onboarding tour', 'SHIP'),
      ]),
    });
    const { onRetry } = await show(noMatch);
    expect(await screen.findByTestId(correctionClosestId(0))).toHaveTextContent(
      'SHIP-5 Onboarding story',
    );
    expect(screen.getByTestId(correctionClosestId(1))).toHaveTextContent('SHIP-6 Onboarding copy');
    expect(screen.queryByTestId(correctionClosestId(2))).toBeNull();
    expect(screen.queryByText(/PAY-9/)).toBeNull();
    const call = api.callsTo('GET /api/search')[0];
    expect(new URL(call.url).searchParams.get('q')).toBe('the onboarding story');

    await fireEvent.press(screen.getByTestId(correctionClosestId(1)));
    expect(onRetry).toHaveBeenCalledWith({ span: { start: 4, end: 24 }, value: 'SHIP-6' });
  });

  it("noMatch offline offers the screen's own closest items without calling search", async () => {
    const api = fakeApi({ 'GET /api/search': view([item('SHIP-5', 'Onboarding story', 'SHIP')]) });
    setConnected(false);
    const spec = specWith({
      rows: [
        { key: 'SHIP-1', title: 'Billing' },
        { key: 'SHIP-2', title: 'The onboarding story copy' },
        { key: 'SHIP-3', title: 'Onboarding' },
        { key: 'SHIP-4', title: 'Onboarding story tour' },
      ],
    });
    const { onRetry } = await show(noMatch, { spec });
    expect(screen.getByTestId(correctionClosestId(0))).toHaveTextContent(
      'SHIP-2 The onboarding story copy',
    );
    expect(screen.getByTestId(correctionClosestId(1))).toHaveTextContent(
      'SHIP-4 Onboarding story tour',
    );
    expect(screen.queryByTestId(correctionClosestId(2))).toBeNull();
    expect(api.callsTo('GET /api/search')).toHaveLength(0);
    await fireEvent.press(screen.getByTestId(correctionClosestId(0)));
    expect(onRetry).toHaveBeenCalledWith({ span: { start: 4, end: 24 }, value: 'SHIP-2' });
  });

  it('misheardKey offers exactly one key and Yes retries with it', async () => {
    const { onRetry } = await show(
      { kind: 'misheardKey', detail: { spoken: 'SHIP-960', suggestion: 'SHIP-906' } },
      { heard: 'Move SHIP-960 up to second' },
    );
    expect(screen.getByTestId(correctionFailureYesId)).toHaveTextContent('Yes, SHIP-906');
    expect(screen.getByText("There's no SHIP-960.")).toBeOnTheScreen();
    expect(screen.queryByTestId(correctionClosestId(0))).toBeNull();
    expect(screen.queryByTestId(correctionCandidateId(0))).toBeNull();
    await fireEvent.press(screen.getByTestId(correctionFailureYesId));
    expect(onRetry).toHaveBeenCalledWith({ span: { start: 5, end: 13 }, value: 'SHIP-906' });
  });

  it('ambiguous numbers the candidates in rank order and a tap retries with that key', async () => {
    const { onRetry } = await show({
      kind: 'ambiguous',
      detail: {
        candidates: [
          { key: 'SHIP-912', title: 'Sign-in copy', rank: 2 },
          { key: 'SHIP-906', title: 'Sign-in', rank: 1 },
        ],
        call: { action: 'move', args: {} },
      },
    });
    expect(screen.getByTestId(correctionCandidateId(0))).toHaveTextContent('1 · SHIP-906 Sign-in');
    expect(screen.getByTestId(correctionCandidateId(1))).toHaveTextContent(
      '2 · SHIP-912 Sign-in copy',
    );
    await fireEvent.press(screen.getByTestId(correctionCandidateId(1)));
    expect(onRetry).toHaveBeenCalledWith({
      span: { start: 0, end: HEARD.length },
      value: 'SHIP-912',
    });
  });

  it("noSuchState offers the type's states as chips and a tap retries with that state", async () => {
    const spec = specWith({ states: ['Proposed', 'Active', 'Closed'] });
    const { onRetry } = await show(
      { kind: 'noValidAction', detail: { action: 'setState', reason: 'noSuchState' } },
      { spec, heard: 'Set this to Done' },
    );
    expect(screen.getByTestId(correctionStateId('Proposed'))).toBeOnTheScreen();
    expect(screen.getByTestId(correctionStateId('Closed'))).toBeOnTheScreen();
    expect(screen.queryByTestId(correctionStateId('Done'))).toBeNull();
    await fireEvent.press(screen.getByTestId(correctionStateId('Active')));
    expect(onRetry).toHaveBeenCalledWith({ span: { start: 0, end: 16 }, value: 'Active' });
  });

  it('noSuchState with no states in the spec shows no chips', async () => {
    await show({ kind: 'noValidAction', detail: { action: 'setState', reason: 'noSuchState' } });
    expect(screen.queryByTestId(correctionStateId('Active'))).toBeNull();
    expect(screen.getByTestId(correctionFailureWhatCanISayId)).toBeOnTheScreen();
  });

  it('webOnly shows where and opens the item on the web', async () => {
    await show(
      { kind: 'webOnly', detail: { action: 'moveTask', where: 'Tasks move on the web' } },
      {
        spec: specWith({ key: 'SHIP-5' }),
        projectKey: 'SHIP',
        heard: 'Move the checksum task to Done',
      },
    );
    expect(screen.getByText('Tasks move on the web')).toBeOnTheScreen();
    await fireEvent.press(screen.getByTestId(correctionFailureOpenWebId));
    expect(openBrowserAsync).toHaveBeenCalledWith(`${platform().apiOrigin()}/p/SHIP/items/SHIP-5`);
  });

  it('passes the unmatched span to HeardQuote and says nothing was changed', async () => {
    const { voice } = await show(noMatch);
    expect(screen.getByTestId(heardQuoteMissId(heardQuoteId))).toHaveTextContent(
      'the onboarding story',
    );
    expect(screen.getByTestId(heardQuoteTextId(heardQuoteId))).toHaveTextContent(`“${HEARD}”`);
    expect(screen.getByTestId(correctionFailureNothingChangedId)).toHaveTextContent(
      'Nothing was changed',
    );
    expect(screen.getByTestId(correctionFailureFixId)).toHaveTextContent('Fix');
    await fireEvent.press(screen.getByTestId(correctionFailureFixId));
    expect(voice.calls).toEqual(['fix']);
  });

  it('typed shows Edit instead of Fix', async () => {
    await show(noMatch, { source: 'typed' });
    expect(screen.getByTestId(correctionFailureFixId)).toHaveTextContent('Edit');
  });
});
