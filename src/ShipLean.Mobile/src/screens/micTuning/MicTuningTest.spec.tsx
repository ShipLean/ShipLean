import { act, fireEvent, screen } from '@testing-library/react-native';
import { Platform } from 'react-native';
import { installFakeEngines } from '../../test-support/voiceUi';
import { renderScreen } from '../../test-support/renderScreen';
import { PHONE_INPUT_ID } from '../../voice/contracts';
import { micTuningResult } from '../../voice/micTuning/events';
import { classify, type TuningResult, type TuningResultKind } from '../../voice/micTuning/line';
import { requestTypeInstead, takeTypeInstead } from '../../voice/micTuning/returnIntent';
import { MicTuningTest } from './MicTuningTest';
import { micTuningTestIds as ids } from './MicTuningTest.testIds';

const mockBack = jest.fn();
jest.mock('expo-router', () => ({
  ...jest.requireActual<object>('expo-router'),
  useRouter: () => ({ back: mockBack }),
  useFocusEffect: (effect: () => (() => void) | undefined) => {
    jest.requireActual<typeof import('react')>('react').useEffect(effect, [effect]);
  },
}));
jest.mock('../../voice/micTuning/events', () => ({
  micTuningOpened: jest.fn(),
  micTuningResult: jest.fn(),
}));
// The real classify is task B's; these specs assert what the UI does with its answer.
jest.mock('../../voice/micTuning/line', () => ({
  ...jest.requireActual<object>('../../voice/micTuning/line'),
  classify: jest.fn(),
  lineToSlider: () => 7,
}));

const NOW = new Date('2026-11-12T09:41:00.000Z');
const now = () => NOW;

const answer = (kind: TuningResultKind): TuningResult => ({
  kind,
  roomRms: 40,
  voiceRms: 900,
  lineRms: 220,
});

const RESULT_ID: Record<TuningResultKind, string> = {
  good: ids.resultGood,
  tooQuiet: ids.resultTooQuiet,
  noisy: ids.resultNoisy,
  noSignal: ids.resultNoSignal,
};

beforeEach(() => {
  jest.useFakeTimers();
  mockBack.mockClear();
  jest.mocked(micTuningResult).mockClear();
  takeTypeInstead();
});
afterEach(() => jest.useRealTimers());

async function toSpeak(props: { from?: 'third_hold' | 'settings'; itemKey?: string | null } = {}) {
  const fakes = installFakeEngines();
  const itemKey = props.itemKey === null ? undefined : (props.itemKey ?? 'SHIP-1234');
  await renderScreen(
    <MicTuningTest from={props.from ?? 'third_hold'} itemKey={itemKey} now={now} />,
  );
  await act(async () => {});
  await fireEvent.press(screen.getByTestId(ids.startButton));
  await act(async () => {
    jest.advanceTimersByTime(3000);
  });
  return fakes;
}

async function holdAndRelease() {
  await fireEvent(screen.getByTestId(ids.hold), 'pressIn');
  await act(async () => {});
  await fireEvent(screen.getByTestId(ids.hold), 'pressOut');
  await act(async () => {});
}

describe('MicTuningTest results', () => {
  it.each<TuningResultKind>(['good', 'tooQuiet', 'noisy', 'noSignal'])(
    'shows the %s result for what classify answers, and reports it',
    async (kind) => {
      jest.mocked(classify).mockReturnValue(answer(kind));
      const fakes = await toSpeak();
      fakes.recognizer.script([], 'move ship 1234 to done', [], [900, 900]);
      await holdAndRelease();

      expect(screen.getByTestId(RESULT_ID[kind])).toBeOnTheScreen();
      expect(micTuningResult).toHaveBeenCalledWith(kind, Platform.OS);
    },
  );

  it.each<TuningResultKind>(['good', 'tooQuiet'])(
    '%s saves under the input asked for and warns when the phone routed another',
    async (kind) => {
      jest.mocked(classify).mockReturnValue(answer(kind));
      const fakes = await toSpeak();
      fakes.recognizer.setInputInUse({
        id: 'bt-1',
        kind: 'bluetooth',
        name: 'Buds',
        connected: true,
      });
      await holdAndRelease();

      expect(fakes.tuning.tuning(PHONE_INPUT_ID)).toEqual({
        inputId: PHONE_INPUT_ID,
        roomRms: 40,
        voiceRms: 900,
        lineRms: 220,
        testedAt: NOW.toISOString(),
        automatic: true,
        manualStep: null,
      });
      expect(fakes.tuning.tuning('bt-1')).toBeNull();
      expect(screen.getByTestId(ids.wrongInput)).toHaveTextContent(
        'Wrong input: the test asked for Phone, but the phone recorded from Buds.',
      );
    },
  );

  it('shows no wrong-input line when the routed input is the one asked for', async () => {
    jest.mocked(classify).mockReturnValue(answer('good'));
    const fakes = await toSpeak();
    fakes.recognizer.setInputInUse({
      id: PHONE_INPUT_ID,
      kind: 'phone',
      name: 'Phone',
      connected: true,
    });
    await holdAndRelease();
    expect(screen.queryByTestId(ids.wrongInput)).toBeNull();
  });

  it('ends the test when the decode fails', async () => {
    jest.mocked(classify).mockReturnValue(answer('good'));
    const fakes = await toSpeak();
    jest.spyOn(fakes.recognizer, 'start').mockResolvedValue({
      onPartial: () => {},
      onLevel: () => {},
      frames: () => [],
      stop: () => Promise.reject(new Error('decode failed')),
      cancel: () => {},
    });
    await holdAndRelease();

    expect(screen.queryByTestId(ids.stepSpeak)).toBeNull();
    expect(screen.getByTestId(ids.notReady)).toBeOnTheScreen();
    expect(fakes.tuning.tuning(PHONE_INPUT_ID)).toBeNull();
  });

  it('saves for the phone when no input is reported', async () => {
    jest.mocked(classify).mockReturnValue(answer('good'));
    const fakes = await toSpeak();
    await holdAndRelease();

    expect(fakes.tuning.tuning(PHONE_INPUT_ID)?.lineRms).toBe(220);
  });

  it.each<TuningResultKind>(['noisy', 'noSignal'])('%s saves nothing', async (kind) => {
    jest.mocked(classify).mockReturnValue(answer(kind));
    const fakes = await toSpeak();
    await holdAndRelease();

    expect(fakes.tuning.tuning(PHONE_INPUT_ID)).toBeNull();
  });

  it('good shows what was heard and the sensitivity from the line', async () => {
    jest.mocked(classify).mockReturnValue(answer('good'));
    const fakes = await toSpeak();
    fakes.recognizer.script([], 'Move SHIP-1234 to Done');
    await holdAndRelease();

    expect(screen.getByTestId(ids.heard)).toHaveTextContent(/Move SHIP-1234 to Done/);
    expect(screen.getByTestId(ids.sensitivity)).toHaveTextContent('Sensitivity Automatic 7 of 10');
  });

  it('hands both steps frames to classify', async () => {
    jest.mocked(classify).mockReturnValue(answer('good'));
    const fakes = installFakeEngines();
    fakes.recognizer.script([], '', [], [1, 2, 3]);
    await renderScreen(<MicTuningTest from="third_hold" itemKey="SHIP-1" now={now} />);
    await act(async () => {});
    await fireEvent.press(screen.getByTestId(ids.startButton));
    await act(async () => {
      jest.advanceTimersByTime(3000);
    });
    fakes.recognizer.script([], '', [], [9, 9]);
    await holdAndRelease();

    expect(classify).toHaveBeenLastCalledWith([1, 2, 3], [9, 9]);
  });

  it('Try again returns to the start', async () => {
    jest.mocked(classify).mockReturnValue(answer('good'));
    await toSpeak();
    await holdAndRelease();
    await fireEvent.press(screen.getByTestId(ids.tryAgain));

    expect(screen.getByTestId(ids.startButton)).toBeOnTheScreen();
  });
});

describe('MicTuningTest steps', () => {
  it('step 1 listens with no hotwords and ends at 3 s', async () => {
    const fakes = installFakeEngines();
    await renderScreen(<MicTuningTest from="third_hold" itemKey="SHIP-1" now={now} />);
    await act(async () => {});
    await fireEvent.press(screen.getByTestId(ids.startButton));
    await act(async () => {});

    expect(screen.getByTestId(ids.stepQuiet)).toBeOnTheScreen();
    expect(fakes.recognizer.starts.at(-1)).toEqual({ hotwords: [] });
    await act(async () => {
      jest.advanceTimersByTime(2999);
    });
    expect(screen.getByTestId(ids.stepQuiet)).toBeOnTheScreen();
    expect(screen.getByTestId(ids.countdown)).toHaveTextContent('1 s');
    await act(async () => {
      jest.advanceTimersByTime(1);
    });
    expect(screen.getByTestId(ids.stepSpeak)).toBeOnTheScreen();
  });

  it('step 2 shows the phrase with the key, and partials as they come', async () => {
    const fakes = installFakeEngines();
    await renderScreen(<MicTuningTest from="third_hold" itemKey="SHIP-1234" now={now} />);
    await act(async () => {});
    await fireEvent.press(screen.getByTestId(ids.startButton));
    await act(async () => {
      jest.advanceTimersByTime(3000);
    });
    fakes.recognizer.script(['Move SHIP-1234 to Do'], 'Move SHIP-1234 to Done');
    await fireEvent(screen.getByTestId(ids.hold), 'pressIn');
    await act(async () => {});

    expect(screen.getByTestId(ids.phrase)).toHaveTextContent(/Move SHIP-1234 to Done/);
    expect(screen.getByTestId(ids.partial)).toHaveTextContent('Move SHIP-1234 to Do');
    expect(fakes.recognizer.starts.at(-1)).toEqual({
      hotwords: ['SHIP-1234', 'Move', 'Done'],
      decodeAlways: true,
    });
  });

  it('without a key the phrase is Show my inbox', async () => {
    await toSpeak({ itemKey: null });

    expect(screen.getByTestId(ids.phrase)).toHaveTextContent(/Show my inbox/);
  });

  it('shows that voice is not ready instead of the test when the recognizer cannot start', async () => {
    const fakes = installFakeEngines();
    jest.spyOn(fakes.recognizer, 'start').mockRejectedValue(new Error('no model'));
    await renderScreen(<MicTuningTest from="third_hold" now={now} />);
    await act(async () => {});

    expect(screen.getByTestId(ids.notReady)).toBeOnTheScreen();
    expect(screen.queryByTestId(ids.startButton)).toBeNull();
  });
});

describe('MicTuningTest no signal causes', () => {
  const original = Platform.OS;
  afterEach(() => {
    Platform.OS = original;
  });

  async function noSignalOn(os: 'android' | 'ios') {
    Platform.OS = os;
    jest.mocked(classify).mockReturnValue(answer('noSignal'));
    await toSpeak();
    await holdAndRelease();
  }

  it('lists the Android causes in order', async () => {
    await noSignalOn('android');

    expect(screen.getByTestId(ids.cause(1))).toHaveTextContent(/Microphone access is off/);
    expect(screen.getByTestId(ids.cause(2))).toHaveTextContent(/Another app has the microphone/);
    expect(screen.getByTestId(ids.cause(3))).toHaveTextContent(/Something covers the mic/);
  });

  it('lists the iOS causes in order', async () => {
    await noSignalOn('ios');

    expect(screen.getByTestId(ids.cause(1))).toHaveTextContent(/Another app has the microphone/);
    expect(screen.getByTestId(ids.cause(2))).toHaveTextContent(/Something covers the mic/);
    expect(screen.getByTestId(ids.cause(3))).toHaveTextContent(/Screen Time/);
  });
});

describe('MicTuningTest Type instead', () => {
  it('asks for the composer and goes back', async () => {
    installFakeEngines();
    await renderScreen(<MicTuningTest from="third_hold" itemKey="SHIP-1" now={now} />);
    await act(async () => {});
    await fireEvent.press(screen.getByTestId(ids.typeInstead));

    expect(takeTypeInstead()).toBe(true);
    expect(mockBack).toHaveBeenCalledTimes(1);
  });

  it('is hidden when the page came from settings', async () => {
    installFakeEngines();
    await renderScreen(<MicTuningTest from="settings" now={now} />);
    await act(async () => {});

    expect(screen.queryByTestId(ids.typeInstead)).toBeNull();
    expect(requestTypeInstead).toBeDefined();
  });
});
