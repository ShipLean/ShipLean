import { act, fireEvent, screen } from '@testing-library/react-native';
import { fakeVoice, renderWithVoice } from '../../test-support/voiceUi';
import { CommandComposer } from './CommandComposer';
import {
  commandComposerFieldId,
  commandComposerGreyedId,
  commandComposerPreviewId,
  commandComposerReasonId,
  commandComposerRouteId,
} from './CommandComposer.testIds';
import type { ComposerReason } from './composerRequest';

function composer(text: string, reason: ComposerReason = null) {
  const onChangeText = jest.fn();
  const onSent = jest.fn();
  return {
    onChangeText,
    onSent,
    ui: <CommandComposer text={text} onChangeText={onChangeText} reason={reason} onSent={onSent} />,
  };
}

describe('CommandComposer', () => {
  it('sends a Command to submitTyped', async () => {
    const c = composer('set this to active');
    const voice = fakeVoice();
    await renderWithVoice(c.ui, { voice });
    expect(screen.getByTestId(commandComposerRouteId)).toHaveTextContent(
      'Preview first · nothing changes until you apply',
    );
    await fireEvent.changeText(screen.getByTestId(commandComposerFieldId), 'set this to active!');
    expect(c.onChangeText).toHaveBeenCalledWith('set this to active!');

    const send = screen.getByTestId(commandComposerPreviewId);
    expect(send).toHaveTextContent('Preview');
    expect(send).toBeEnabled();
    await fireEvent.press(send);
    expect(voice.calls).toEqual(['submitTyped("set this to active")']);
    expect(c.onSent).toHaveBeenCalledTimes(1);
  });

  it('focuses the field on mount', async () => {
    await renderWithVoice(composer('').ui);
    expect(screen.getByTestId(commandComposerFieldId)).toHaveProp('autoFocus', true);
  });

  it('shows the reason line for micOff and none for edit', async () => {
    const view = await renderWithVoice(composer('', 'micOff').ui);
    expect(screen.getByTestId(commandComposerReasonId)).toHaveTextContent(
      'Microphone is off · typing instead',
    );
    await view.rerender(composer('set this to active', 'edit').ui);
    expect(screen.queryByTestId(commandComposerReasonId)).not.toBeOnTheScreen();
  });

  it('greys Preview with the reason while the models download', async () => {
    const c = composer('set this to active');
    const voice = fakeVoice({ kind: 'disabled', reason: 'downloading', permission: 'granted' });
    await renderWithVoice(c.ui, { voice });
    expect(screen.getByTestId(commandComposerGreyedId)).toHaveTextContent(
      'Command needs the voice models',
    );
    const send = screen.getByTestId(commandComposerPreviewId);
    expect(send).toBeDisabled();
    await fireEvent.press(send);
    expect(voice.calls).toEqual([]);
    expect(c.onSent).not.toHaveBeenCalled();

    // The mic being off doesn't grey Command: a typed command doesn't need it.
    await act(() => voice.set({ kind: 'disabled', reason: 'micOff', permission: 'deniedFinal' }));
    expect(screen.queryByTestId(commandComposerGreyedId)).not.toBeOnTheScreen();
    expect(screen.getByTestId(commandComposerPreviewId)).toBeEnabled();
  });

  it('disables Preview for blank text', async () => {
    const c = composer('   ');
    const voice = fakeVoice();
    await renderWithVoice(c.ui, { voice });
    const send = screen.getByTestId(commandComposerPreviewId);
    expect(send).toBeDisabled();
    await fireEvent.press(send);
    expect(voice.calls).toEqual([]);
    expect(c.onSent).not.toHaveBeenCalled();
  });
});
