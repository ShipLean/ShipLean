import { act, fireEvent, screen } from '@testing-library/react-native';
import { router } from 'expo-router';
import { links } from '../../links';
import { renderApp } from '../../test-support/renderApp';
import { micTuningOpened } from '../../voice/micTuning/events';
import { screenIds } from '../testIds';
import { adjustByHandId } from './AdjustByHand.testIds';
import { inputCardId } from './InputCard.testIds';
import { micTuningBackId } from './MicTuningScreen.testIds';
import { micTuningTestId } from './MicTuningTest.testIds';

jest.mock('../../voice/micTuning/events', () => ({
  micTuningOpened: jest.fn(),
  micTuningResult: jest.fn(),
}));

beforeEach(() => jest.mocked(micTuningOpened).mockClear());

describe('MicTuningScreen', () => {
  it('shows the root and its three children, with the key in the back label', async () => {
    await renderApp({ url: '/mic-tuning?from=settings&key=SHIP-7' });

    expect(await screen.findByTestId(screenIds.micTuning)).toBeOnTheScreen();
    expect(screen.getByTestId(inputCardId)).toBeOnTheScreen();
    expect(screen.getByTestId(micTuningTestId)).toBeOnTheScreen();
    expect(screen.getByTestId(adjustByHandId)).toBeOnTheScreen();
    expect(screen.getByTestId(micTuningBackId)).toHaveTextContent('Back to SHIP-7');
  });

  it('says plain Back without a key', async () => {
    await renderApp({ url: '/mic-tuning?from=settings' });

    expect(await screen.findByTestId(micTuningBackId)).toHaveTextContent(/^Back$/);
  });

  it('goes back to where it was pushed from', async () => {
    await renderApp();
    await act(async () => router.push(links.micTuning('third_hold', 'SHIP-7')));
    expect(await screen.findByTestId(screenIds.micTuning)).toBeOnTheScreen();

    await act(async () => fireEvent.press(screen.getByTestId(micTuningBackId)));

    expect(screen.queryByTestId(screenIds.micTuning)).toBeNull();
    expect(screen.getByTestId(screenIds.projects)).toBeOnTheScreen();
  });

  it('reports the page opened once, with where it came from, however often it renders', async () => {
    await renderApp({ url: '/mic-tuning?from=settings&key=SHIP-7' });
    await screen.findByTestId(screenIds.micTuning);

    // The params change while the page is open, where it came from included.
    await act(async () => router.setParams({ key: 'SHIP-8', from: 'third_hold' }));

    expect(micTuningOpened).toHaveBeenCalledTimes(1);
    expect(micTuningOpened).toHaveBeenCalledWith('settings');
  });
});
