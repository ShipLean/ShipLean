import { screen } from '@testing-library/react-native';
import { renderScreen } from '../test-support/renderScreen';
import { themes } from '../theme';
import { ProgressBar } from './ProgressBar';
import { primitiveIds } from './testIds';

describe.each(['light', 'dark'] as const)('ProgressBar (%s)', (scheme) => {
  it('sizes done, current and other sprint over story-level work only', async () => {
    // Story level: 8 open + 2 closed = 10; the Feature's rollup is left out.
    await renderScreen(
      <ProgressBar
        rollups={[
          { type: 'story', open: 6, closed: 2, inCurrentSprint: 3, inOtherSprint: 1 },
          { type: 'bug', open: 2, closed: 0, inCurrentSprint: 1, inOtherSprint: 0 },
          { type: 'feature', open: 5, closed: 5, inCurrentSprint: 5, inOtherSprint: 5 },
        ]}
      />,
      { scheme },
    );

    const { colors } = themes[scheme];
    expect(screen.getByTestId(primitiveIds.progressDone)).toHaveStyle({
      width: '20%',
      backgroundColor: colors.ok,
    });
    expect(screen.getByTestId(primitiveIds.progressCurrent)).toHaveStyle({
      width: '40%',
      backgroundColor: colors.progressCurrentSprint,
    });
    expect(screen.getByTestId(primitiveIds.progressOther)).toHaveStyle({
      width: '10%',
      backgroundColor: colors.progressOtherSprint,
    });
  });

  it('shows only the track when there is no story-level work', async () => {
    await renderScreen(<ProgressBar rollups={[]} />, { scheme });

    expect(screen.getByTestId(primitiveIds.progressBar)).toHaveStyle({
      backgroundColor: themes[scheme].colors.surface2,
    });
    expect(screen.queryByTestId(primitiveIds.progressDone)).toBeNull();
    expect(screen.queryByTestId(primitiveIds.progressCurrent)).toBeNull();
    expect(screen.queryByTestId(primitiveIds.progressOther)).toBeNull();
  });
});
