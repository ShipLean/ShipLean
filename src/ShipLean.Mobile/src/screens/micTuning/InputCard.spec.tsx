import { act, fireEvent, screen } from '@testing-library/react-native';
import { renderScreen } from '../../test-support/renderScreen';
import { fakeAudioInputs } from '../../test-support/voice';
import { installFakeEngines } from '../../test-support/voiceUi';
import { PHONE_INPUT_ID, type AudioInput } from '../../voice/contracts';
import { InputCard } from './InputCard';
import { inputCardNameId, inputChangeId, inputOptionId, inputSheetId } from './InputCard.testIds';

const buds: AudioInput = {
  id: 'buds',
  kind: 'bluetooth',
  name: 'Galaxy Buds2 Pro',
  connected: true,
};
const wired: AudioInput = { id: 'wired', kind: 'wired', name: 'Wired headset', connected: false };

async function render() {
  const inputs = fakeAudioInputs([buds, wired]);
  const engines = installFakeEngines({ inputs });
  await renderScreen(<InputCard />);
  return { inputs, tuning: engines.tuning };
}

describe('InputCard', () => {
  it('shows the phone by default', async () => {
    await render();
    expect(screen.getByTestId(inputCardNameId)).toHaveTextContent('Phone · speakerphone');
  });

  it('shows a picked Bluetooth input that is connected', async () => {
    const { tuning } = await render();
    await act(async () => tuning.pick('buds'));
    expect(screen.getByTestId(inputCardNameId)).toHaveTextContent('Galaxy Buds2 Pro');
  });

  it('falls back to the phone when the picked input is disconnected, and keeps the pick', async () => {
    const { tuning, inputs } = await render();
    await act(async () => tuning.pick('buds'));
    await act(async () => inputs.disconnect('buds'));

    expect(screen.getByTestId(inputCardNameId)).toHaveTextContent('Phone · speakerphone');
    expect(tuning.pickedInput()).toBe('buds');
  });

  it('re-renders when an input connects after it was picked', async () => {
    const { tuning, inputs } = await render();
    await act(async () => inputs.disconnect('buds'));
    await act(async () => tuning.pick('buds'));
    expect(screen.getByTestId(inputCardNameId)).toHaveTextContent('Phone · speakerphone');

    await act(async () => inputs.connect('buds'));

    expect(screen.getByTestId(inputCardNameId)).toHaveTextContent('Galaxy Buds2 Pro');
  });

  it('lists the inputs in the sheet, greys a disconnected one, and picking calls pick and closes', async () => {
    const { tuning } = await render();
    await fireEvent.press(screen.getByTestId(inputChangeId));

    expect(screen.getByTestId(inputOptionId(PHONE_INPUT_ID))).toHaveTextContent(/default/);
    expect(screen.getByTestId(inputOptionId('wired'))).toBeDisabled();
    expect(screen.getByTestId(inputOptionId('buds'))).toBeEnabled();
    await fireEvent.press(screen.getByTestId(inputOptionId('wired')));
    expect(tuning.pickedInput()).toBe(PHONE_INPUT_ID);

    await fireEvent.press(screen.getByTestId(inputOptionId('buds')));
    expect(tuning.pickedInput()).toBe('buds');
    expect(screen.queryByTestId(inputSheetId)).toBeNull();
  });
});
