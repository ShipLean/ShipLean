import { screen } from '@testing-library/react-native';
import { renderWithVoice } from '../test-support/voiceUi';
import { themes } from '../theme';
import { Dock } from './Dock';
import { primitiveIds } from './testIds';

describe.each(['light', 'dark'] as const)('Dock (%s)', (scheme) => {
  it('shows the field with the placeholder given, at the 15pt body size, and the dock-mic placeholder', async () => {
    await renderWithVoice(<Dock itemId="i1" placeholder="Type a note" />, { scheme });

    const field = screen.getByTestId(primitiveIds.dockField);
    expect(field).toHaveProp('placeholder', 'Type a note');
    expect(field).toHaveProp('editable', false);
    expect(field).toHaveStyle({
      fontSize: 15,
      height: 44,
      backgroundColor: themes[scheme].colors.surface2,
    });
    expect(screen.getByTestId(primitiveIds.dockMic)).toBeOnTheScreen();
  });
});
