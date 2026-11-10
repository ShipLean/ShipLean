import { screen } from '@testing-library/react-native';
import { renderScreen } from '../../test-support/renderScreen';
import { ListeningSheet } from './ListeningSheet';
import {
  listeningContextId,
  listeningHintId,
  listeningHotId,
  listeningPartialId,
} from './ListeningSheet.testIds';
import type { VoiceUiStateOf } from '../internals';

const listening = (partial: string): VoiceUiStateOf<'listening'> => ({
  kind: 'listening',
  partial,
  screen: 'SHIP-906 Voice commands',
  hotwords: ['SHIP-906', 'Active', 'D10'],
});

describe('ListeningSheet', () => {
  it('shows the partial text as it grows and highlights SHIP-906 and Active', async () => {
    const view = await renderScreen(<ListeningSheet state={listening('move ship')} />);
    expect(screen.getByTestId(listeningPartialId)).toHaveTextContent('move ship');
    expect(screen.queryByTestId(listeningHotId)).not.toBeOnTheScreen();

    await view.rerender(<ListeningSheet state={listening('move ship 906 to active now')} />);
    expect(screen.getByTestId(listeningPartialId)).toHaveTextContent('move ship 906 to active now');
    const hot = screen.getAllByTestId(listeningHotId);
    expect(hot.map((h) => h.props.children)).toEqual(['ship 906', 'active']);
  });

  it('says what it applies to and how to send', async () => {
    await renderScreen(<ListeningSheet state={listening('')} />);
    expect(screen.getByTestId(listeningContextId)).toHaveTextContent('SHIP-906 Voice commands');
    expect(screen.getByTestId(listeningHintId)).toHaveTextContent('Release to send');
    expect(screen.getByTestId(listeningPartialId)).toHaveTextContent('Listening…');
  });
});
