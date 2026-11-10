import { act, fireEvent, screen, waitFor } from '@testing-library/react-native';
import { platform } from '@shiplean/core/platform';
import type { ReactElement } from 'react';
import { UNDO } from '../actions/undo';
import { session } from '../session/session';
import { TEST_ORIGIN, TEST_SECRET, aMe, aMember, aProject } from '../test-support/builders';
import { fakeApi } from '../test-support/fakeApi';
import { setConnected } from '../test-support/netInfo';
import { signedInApi } from '../test-support/signedInApi';
import { kvValues } from '../test-support/storage';
import { fakeInternals } from '../test-support/voiceInternals';
import { fakeVoice, renderWithVoice, type FakeVoice } from '../test-support/voiceUi';
import { themes } from '../theme';
import { Dock } from '../ui/Dock';
import { primitiveIds } from '../ui/testIds';
import type { ScreenVoiceSpec } from '../voice/contracts';
import { VoiceInternalsContext } from '../voice/internals';
import type { CommandComposerProps } from '../voice/typed/CommandComposer';
import { commandComposerId } from '../voice/typed/CommandComposer.testIds';
import type { ComposerRequest } from '../voice/typed/composerRequest';
import { dockIds, mentionOptionId } from './testIds';

// The Command side is R2b's: record what the dock hands it.
const mockCommandProps: CommandComposerProps[] = [];
jest.mock('../voice/typed/CommandComposer', () => {
  const { createElement } = jest.requireActual<typeof import('react')>('react');
  const { View } = jest.requireActual<typeof import('react-native')>('react-native');
  const { commandComposerId: id } = jest.requireActual<
    typeof import('../voice/typed/CommandComposer.testIds')
  >('../voice/typed/CommandComposer.testIds');
  return {
    CommandComposer: (props: CommandComposerProps) => {
      mockCommandProps.push(props);
      return createElement(View, { testID: id });
    },
  };
});

// A composer request from Type instead or Edit.
let mockRequest: ComposerRequest | null = null;
const mockConsume = jest.fn();
jest.mock('../voice/typed/composerRequest', () => ({
  ...jest.requireActual<typeof import('../voice/typed/composerRequest')>(
    '../voice/typed/composerRequest',
  ),
  useComposerRequest: () => ({ request: mockRequest, consume: mockConsume }),
}));

const projectId = aProject().id;
const ITEM = { itemId: '0199f000-0000-7000-8000-00000000c001', itemKey: 'SHIP-906' };
const priya = aMember({
  user: {
    id: 'u-priya',
    name: 'Priya Raman',
    initials: 'PR',
    avatarColour: '#1f8f6a',
    photoUrl: null,
  },
});
const pat = aMember({
  user: { id: 'u-pat', name: 'Pat Singh', initials: 'PS', avatarColour: '#2563c9', photoUrl: null },
});
const tom = aMember({
  user: { id: 'u-tom', name: 'Tom Nairn', initials: 'TN', avatarColour: '#c2353b', photoUrl: null },
});

function aSpec(hotwords: string[]): ScreenVoiceSpec {
  return {
    screen: 'workItem',
    actions: [],
    context: () => ({}),
    hotwords: () => hotwords,
    toActions: () => ({
      ok: false,
      failure: { kind: 'noValidAction', detail: { action: null, reason: 'none' } },
    }),
  };
}

interface Options {
  scheme: 'light' | 'dark';
  voice?: FakeVoice;
  hotwords?: string[];
  onSubmitted?: () => void;
}

async function renderDock({ scheme, voice = fakeVoice(), hotwords = [], onSubmitted }: Options) {
  const internals = fakeInternals({ spec: () => aSpec(hotwords) });
  const ui: ReactElement = (
    <VoiceInternalsContext.Provider value={internals}>
      <Dock {...ITEM} projectId={projectId} placeholder="Type a note" onSubmitted={onSubmitted} />
    </VoiceInternalsContext.Provider>
  );
  const result = await renderWithVoice(ui, { scheme, voice });
  // The members read starts with the dock: let it land before the test goes on.
  await waitFor(() => expect(result.queryClient.isFetching()).toBe(0));
  return result;
}

const open = () => fireEvent.press(screen.getByTestId(dockIds.open));
const type = (text: string) => fireEvent.changeText(screen.getByTestId(dockIds.noteField), text);
const disabled = (id: string) => screen.getByTestId(id).props.accessibilityState?.disabled === true;

beforeEach(() => {
  kvValues.clear();
  mockRequest = null;
  mockCommandProps.length = 0;
  fakeApi({
    ...signedInApi(),
    [`GET /api/projects/${projectId}/members`]: [priya, pat, tom],
  });
  session.abandon();
  session.beginSignIn(TEST_ORIGIN);
  session.setPendingSecret(TEST_SECRET);
  session.commit(aMe().email);
});

afterEach(() => {
  setConnected(true);
});

describe.each(['light', 'dark'] as const)('Dock composer (%s)', (scheme) => {
  const { colors } = themes[scheme];

  it('posts a typed note through the runner with the typed undo window', async () => {
    const onSubmitted = jest.fn();
    const { runner } = await renderDock({ scheme, onSubmitted });
    await open();

    expect(screen.getByTestId(dockIds.route)).toHaveTextContent(
      'Posts when you send, word for word, like a comment on the web',
    );
    expect(screen.getByTestId(dockIds.target)).toHaveTextContent('on SHIP-906');
    const words = '  Sam says the fix lands tomorrow.\n@Priya Raman can you rerun it?  ';
    await type(words);
    expect(screen.getByTestId(dockIds.post)).toHaveTextContent('Post');
    expect(screen.getByTestId(dockIds.post)).toHaveStyle({ backgroundColor: colors.accent });
    await fireEvent.press(screen.getByTestId(dockIds.post));

    expect(runner.dispatched).toHaveLength(1);
    expect(runner.dispatched[0]!.batch).toEqual([
      { area: 'workItem', itemId: ITEM.itemId, key: ITEM.itemKey, kind: 'addNote', body: words },
    ]);
    expect(runner.dispatched[0]!.opts).toEqual({
      undo: UNDO.typed,
      origin: 'typed',
      allOrNothing: true,
    });
    expect(onSubmitted).toHaveBeenCalledTimes(1);
    // The field and its draft clear, and the dock closes.
    expect(screen.queryByTestId(dockIds.composer)).toBeNull();
    expect(screen.getByTestId(primitiveIds.dockField)).toHaveProp('value', '');
    expect(screen.queryByTestId(dockIds.draftLabel)).toBeNull();
    expect([...kvValues.keys()].some((k) => k.startsWith('shiplean.dockDraft'))).toBe(false);
  });

  it('posts while offline and leaves the waiting to the runner', async () => {
    const { runner } = await renderDock({ scheme });
    await act(() => setConnected(false));
    expect(platform().isOffline()).toBe(true);

    await open();
    await type('Checked on the Pixel 8 too');
    expect(disabled(dockIds.post)).toBe(false);
    await fireEvent.press(screen.getByTestId(dockIds.post));

    expect(runner.dispatched.map((d) => d.batch)).toEqual([
      [
        {
          area: 'workItem',
          itemId: ITEM.itemId,
          key: ITEM.itemKey,
          kind: 'addNote',
          body: 'Checked on the Pixel 8 too',
        },
      ],
    ]);
    expect(runner.dispatched[0]!.opts.undo).toEqual(UNDO.typed);
  });

  it('keeps a draft when the keyboard closes and restores it on reopen', async () => {
    const first = await renderDock({ scheme });
    await open();
    await type('Sam says the checksum fix lands tomorrow');
    await fireEvent(screen.getByTestId(dockIds.noteField), 'blur');

    // Closed, the dock shows the draft with its mode.
    expect(screen.queryByTestId(dockIds.composer)).toBeNull();
    expect(screen.getByTestId(primitiveIds.dockField)).toHaveProp(
      'value',
      'Sam says the checksum fix lands tomorrow',
    );
    expect(screen.getByTestId(dockIds.draftLabel)).toHaveTextContent('Note draft');

    // Leaving the screen and coming back restores it.
    await first.unmount();
    await renderDock({ scheme });
    expect(screen.getByTestId(dockIds.draftLabel)).toHaveTextContent('Note draft');
    await open();
    expect(screen.getByTestId(dockIds.noteField)).toHaveProp(
      'value',
      'Sam says the checksum fix lands tomorrow',
    );

    // The mode is kept with it.
    await fireEvent.press(screen.getByTestId(dockIds.modeCommand));
    expect(screen.getByTestId(commandComposerId)).toBeOnTheScreen();
    await screen.unmount();
    await renderDock({ scheme });
    expect(screen.getByTestId(dockIds.draftLabel)).toHaveTextContent('Command draft');
    await open();
    expect(screen.queryByTestId(dockIds.noteField)).toBeNull();
    expect(mockCommandProps.at(-1)?.text).toBe('Sam says the checksum fix lands tomorrow');

    // Emptied, it's gone.
    await act(() => mockCommandProps.at(-1)!.onChangeText(''));
    await screen.unmount();
    await renderDock({ scheme });
    expect(screen.queryByTestId(dockIds.draftLabel)).toBeNull();
    expect(screen.getByTestId(primitiveIds.dockField)).toHaveProp('value', '');
  });

  it('shows the nudge from the registered spec hotwords and still posts the note when it is ignored', async () => {
    const voice = fakeVoice();
    const { runner } = await renderDock({ scheme, voice, hotwords: ['SHIP-906', 'D10', 'Active'] });
    await open();

    await type('the S22 runs warm');
    expect(screen.queryByTestId(dockIds.nudge)).toBeNull();

    await type('approve D10 and set this to active');
    expect(screen.getByTestId(dockIds.nudgeTitle)).toHaveTextContent('Looks like 2 changes');
    expect(screen.getByTestId(dockIds.nudge)).toHaveStyle({ backgroundColor: colors.accentSoft });

    // Ignored: Post posts the words as a note, and the mode never switched by itself.
    await fireEvent.press(screen.getByTestId(dockIds.post));
    expect(runner.dispatched.map((d) => d.batch[0])).toEqual([
      expect.objectContaining({ kind: 'addNote', body: 'approve D10 and set this to active' }),
    ]);
    expect(voice.calls).toEqual([]);

    // Taken: Preview instead hands the words to the voice preview.
    await open();
    await type('approve D10');
    await fireEvent.press(screen.getByTestId(dockIds.nudgePreview));
    expect(voice.calls).toEqual(['submitTyped("approve D10")']);
    expect(runner.dispatched).toHaveLength(1);
  });

  it('greys Command with the reason while the models download', async () => {
    const voice = fakeVoice({ kind: 'disabled', reason: 'downloading', permission: 'granted' });
    await renderDock({ scheme, voice });
    await open();

    expect(disabled(dockIds.modeCommand)).toBe(true);
    expect(screen.getByTestId(dockIds.commandOff)).toHaveTextContent(
      'Command needs the voice models',
    );
    await fireEvent.press(screen.getByTestId(dockIds.modeCommand));
    expect(screen.queryByTestId(commandComposerId)).toBeNull();
    // Note still works.
    await type('Checked on the Pixel 8 too');
    expect(disabled(dockIds.post)).toBe(false);

    // A mic that is off leaves Command usable: typing needs no mic.
    await act(() => voice.set({ kind: 'disabled', reason: 'micOff', permission: 'deniedFinal' }));
    expect(disabled(dockIds.modeCommand)).toBe(false);
    expect(screen.queryByTestId(dockIds.commandOff)).toBeNull();
  });

  it('suggests project members after @ and inserts the full name', async () => {
    await renderDock({ scheme });
    await open();

    await type('Ask @na');
    await waitFor(() => expect(screen.getByTestId(dockIds.mentions)).toBeOnTheScreen());
    expect(screen.queryByTestId(mentionOptionId('u-priya'))).toBeNull();
    expect(screen.getByTestId(mentionOptionId('u-tom'))).toHaveTextContent('Tom Nairn');

    await type('Ask @pr');
    await fireEvent.press(screen.getByTestId(mentionOptionId('u-priya')));
    expect(screen.getByTestId(dockIds.noteField)).toHaveProp('value', 'Ask @Priya Raman ');
    expect(screen.queryByTestId(dockIds.mentions)).toBeNull();
  });

  it('disables Post for blank text', async () => {
    const { runner } = await renderDock({ scheme });
    await open();

    expect(disabled(dockIds.post)).toBe(true);
    expect(screen.getByTestId(dockIds.post)).toHaveStyle({ backgroundColor: colors.surface2 });
    await type('  \n ');
    expect(disabled(dockIds.post)).toBe(true);
    await fireEvent.press(screen.getByTestId(dockIds.post));
    expect(runner.dispatched).toEqual([]);
  });

  it('shows CommandComposer on Command', async () => {
    const { runner } = await renderDock({ scheme });
    await open();
    await type('approve D10');
    expect(screen.queryByTestId(commandComposerId)).toBeNull();

    await fireEvent.press(screen.getByTestId(dockIds.modeCommand));
    expect(screen.getByTestId(commandComposerId)).toBeOnTheScreen();
    // Command's route line and Preview are CommandComposer's: the dock's Note line and Post go.
    expect(screen.queryByTestId(dockIds.noteField)).toBeNull();
    expect(screen.queryByTestId(dockIds.route)).toBeNull();
    expect(screen.queryByTestId(dockIds.post)).toBeNull();
    expect(mockCommandProps.at(-1)).toMatchObject({ text: 'approve D10', reason: null });

    // The switch remembers the choice while the screen is open.
    await act(() => mockCommandProps.at(-1)!.onSent());
    expect(screen.queryByTestId(dockIds.composer)).toBeNull();
    await open();
    expect(screen.getByTestId(commandComposerId)).toBeOnTheScreen();
    expect(runner.dispatched).toEqual([]);
  });

  it('opens on Command with the text and reason from a composer request', async () => {
    mockRequest = {
      mode: 'command',
      text: 'give this to Priya',
      reason: 'micOff',
      questionNumber: null,
    };
    await renderDock({ scheme });

    expect(screen.getByTestId(dockIds.composer)).toBeOnTheScreen();
    expect(screen.getByTestId(commandComposerId)).toBeOnTheScreen();
    expect(mockCommandProps.at(-1)).toMatchObject({ text: 'give this to Priya', reason: 'micOff' });
    expect(mockConsume).toHaveBeenCalledTimes(1);
  });
});
