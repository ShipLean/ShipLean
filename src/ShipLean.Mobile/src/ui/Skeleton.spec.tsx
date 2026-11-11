import { screen } from '@testing-library/react-native';
import { renderScreen } from '../test-support/renderScreen';
import { Skeleton } from './Skeleton';
import { primitiveIds } from './testIds';

describe.each(['light', 'dark'] as const)('Skeleton (%s)', (scheme) => {
  it('draws the number of rows asked for', async () => {
    await renderScreen(<Skeleton rows={4} />, { scheme });

    expect(screen.getByTestId(primitiveIds.skeleton)).toBeOnTheScreen();
    expect(screen.getAllByTestId(primitiveIds.skeletonRow)).toHaveLength(4);
  });
});
