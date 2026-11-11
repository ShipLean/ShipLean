import { act, fireEvent, screen } from '@testing-library/react-native';
import { renderScreen } from '../../test-support/renderScreen';
import { installFakeEngines } from '../../test-support/voiceUi';
import { PHONE_INPUT_ID, type MicTuning } from '../../voice/contracts';
import { sliderToLine } from '../../voice/micTuning/line';
import { AdjustByHand } from './AdjustByHand';
import {
  adjustAutomaticId,
  adjustMeterId,
  adjustSaveId,
  adjustStepId,
  adjustToggleId,
} from './AdjustByHand.testIds';

jest.mock('../../voice/micTuning/line', () => ({
  ...jest.requireActual<typeof import('../../voice/micTuning/line')>('../../voice/micTuning/line'),
  sliderToLine: jest.fn((step: number) => step * 100),
  lineToSlider: jest.fn(() => 5),
}));

jest.mock('./useLiveLevel', () => ({ useLiveLevel: () => ({ level: 0.4, failed: false }) }));

const tested: MicTuning = {
  inputId: PHONE_INPUT_ID,
  roomRms: 40,
  voiceRms: 900,
  lineRms: 190,
  testedAt: '2026-11-12T08:00:00.000Z',
  automatic: true,
  manualStep: null,
};

async function render(initial: MicTuning | null = tested) {
  const engines = installFakeEngines();
  if (initial !== null) engines.tuning.save(initial);
  await renderScreen(<AdjustByHand />);
  await fireEvent.press(screen.getByTestId(adjustToggleId));
  return engines.tuning;
}

describe('AdjustByHand', () => {
  it('is collapsed until opened, with Automatic on', async () => {
    installFakeEngines();
    await renderScreen(<AdjustByHand />);
    expect(screen.queryByTestId(adjustMeterId)).toBeNull();
    await fireEvent.press(screen.getByTestId(adjustToggleId));
    expect(screen.getByTestId(adjustAutomaticId).props.value).toBe(true);
    expect(screen.queryByTestId(adjustStepId(3))).toBeNull();
  });

  it('with Automatic off, Save writes manualStep and keeps room, voice and line', async () => {
    const tuning = await render();
    await fireEvent(screen.getByTestId(adjustAutomaticId), 'valueChange', false);
    await fireEvent.press(screen.getByTestId(adjustStepId(7)));
    expect(sliderToLine).toHaveBeenCalledWith(7);
    await fireEvent.press(screen.getByTestId(adjustSaveId));

    expect(tuning.tuning(PHONE_INPUT_ID)).toEqual({ ...tested, automatic: false, manualStep: 7 });
  });

  it('turning Automatic back on restores automatic and the tested line', async () => {
    const tuning = await render({ ...tested, automatic: false, manualStep: 7 });
    expect(screen.getByTestId(adjustAutomaticId).props.value).toBe(false);
    await act(async () => fireEvent(screen.getByTestId(adjustAutomaticId), 'valueChange', true));

    expect(tuning.tuning(PHONE_INPUT_ID)).toEqual({ ...tested, automatic: true, manualStep: null });
  });

  it('a manual save without a test uses the default floor for room and voice', async () => {
    const tuning = await render(null);
    await fireEvent(screen.getByTestId(adjustAutomaticId), 'valueChange', false);
    await fireEvent.press(screen.getByTestId(adjustStepId(2)));
    await fireEvent.press(screen.getByTestId(adjustSaveId));

    expect(tuning.tuning(PHONE_INPUT_ID)).toMatchObject({
      roomRms: 300,
      voiceRms: 300,
      lineRms: 300,
      automatic: false,
      manualStep: 2,
    });
  });
});
