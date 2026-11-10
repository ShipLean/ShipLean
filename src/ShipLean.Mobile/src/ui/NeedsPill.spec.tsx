import { screen } from '@testing-library/react-native';
import { renderScreen } from '../test-support/renderScreen';
import { NeedsPill } from './NeedsPill';
import { primitiveIds } from './testIds';

describe.each(['light', 'dark'] as const)('NeedsPill (%s)', (scheme) => {
  it('is hidden at 0', async () => {
    await renderScreen(<NeedsPill needs={{ questions: 0, decisions: 0 }} />, { scheme });

    expect(screen.queryByTestId(primitiveIds.needsPill)).toBeNull();
  });

  it('is hidden for null', async () => {
    await renderScreen(<NeedsPill needs={null} />, { scheme });

    expect(screen.queryByTestId(primitiveIds.needsPill)).toBeNull();
  });

  it('counts questions and decisions together', async () => {
    await renderScreen(<NeedsPill needs={{ questions: 2, decisions: 1 }} />, { scheme });

    expect(screen.getByTestId(primitiveIds.needsPill)).toHaveTextContent('3');
  });
});
