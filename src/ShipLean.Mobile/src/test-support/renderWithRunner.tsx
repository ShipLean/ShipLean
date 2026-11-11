// Its own file: vitest runs in Node and can't load React Native, so actions.ts stays pure.
import type { ReactElement } from 'react';
import { ActionRunnerProvider } from '../actions/ActionRunnerProvider';
import type { ActionRunner } from '../actions/types';
import type { Scheme } from '../theme/themes';
import { fakeRunner, type FakeRunner } from './actions';
import { renderScreen } from './renderScreen';

export interface RenderWithRunnerOptions<R extends ActionRunner> {
  runner?: R;
  scheme?: Scheme;
}

// renderScreen inside an ActionRunnerProvider; the runner is a fakeRunner() unless one is given.
export async function renderWithRunner<R extends ActionRunner = FakeRunner>(
  ui: ReactElement,
  { runner, scheme }: RenderWithRunnerOptions<R> = {},
) {
  const used = (runner ?? fakeRunner()) as R;
  const result = await renderScreen(
    <ActionRunnerProvider runner={used}>{ui}</ActionRunnerProvider>,
    { scheme },
  );
  return Object.assign(result, { runner: used });
}
