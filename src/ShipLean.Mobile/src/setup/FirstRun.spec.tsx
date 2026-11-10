import { act, screen } from '@testing-library/react-native';
import { router } from 'expo-router';
import { projectRowId } from '../screens/projects/testIds';
import { screenIds, screenSetup } from '../screens/testIds';
import { renderApp } from '../test-support/renderApp';
import { kvValues } from '../test-support/storage';
import { FakeModelStore } from '../test-support/voice';
import { installFakeEngines } from '../test-support/voiceUi';
import type { ModelStatus } from '../voice/contracts';
import { SETUP_OFFERED_KEY } from './firstRun';

function aPhone(status: Partial<ModelStatus>) {
  const models = new FakeModelStore();
  models.set(status);
  models.setMemory({ reportedGb: 11.2, soldGb: 12 });
  return installFakeEngines({ models });
}

beforeEach(() => kvValues.delete(SETUP_OFFERED_KEY));

describe('firstRun', () => {
  it('opens setup once after the first sign-in, and never after voice was set up or skipped', async () => {
    aPhone({ phase: 'none' });
    await renderApp();

    expect(await screen.findByTestId(screenSetup('models'))).toBeOnTheScreen();
    expect(kvValues.get(SETUP_OFFERED_KEY)).toBe('1');

    // Back to Projects: it doesn't open again, in this run or the next.
    await act(() => router.dismissTo('/'));
    expect(await screen.findByTestId(projectRowId('SHIP'))).toBeOnTheScreen();
    expect(screen.queryByTestId(screenSetup('models'))).toBeNull();
  });

  it('does not open again on the next launch once it was offered', async () => {
    kvValues.set(SETUP_OFFERED_KEY, '1');
    aPhone({ phase: 'none' });
    await renderApp();

    expect(await screen.findByTestId(projectRowId('SHIP'))).toBeOnTheScreen();
    expect(screen.queryByTestId(screenSetup('models'))).toBeNull();
    expect(screen.getByTestId(screenIds.projects)).toBeOnTheScreen();
  });

  it.each<Partial<ModelStatus>>([
    { phase: 'skipped' },
    { phase: 'downloading' },
    { phase: 'paused' },
    { phase: 'ready' },
  ])('stays on Projects when voice is already %j', async (status) => {
    aPhone(status);
    await renderApp();

    expect(await screen.findByTestId(projectRowId('SHIP'))).toBeOnTheScreen();
    expect(screen.queryByTestId(screenSetup('models'))).toBeNull();
    expect(kvValues.get(SETUP_OFFERED_KEY)).toBeUndefined();
  });

  it('stays on Projects when the engines are not configured', async () => {
    await renderApp();

    expect(await screen.findByTestId(projectRowId('SHIP'))).toBeOnTheScreen();
    expect(screen.queryByTestId(screenSetup('models'))).toBeNull();
  });
});
