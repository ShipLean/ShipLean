import { fireEvent, screen } from '@testing-library/react-native';
import { renderScreen } from '../test-support/renderScreen';
import { themes } from '../theme';
import { Banner, type BannerTone } from './Banner';
import { tint } from './layout';
import { actionId, primitiveIds } from './testIds';

describe.each(['light', 'dark'] as const)('Banner (%s)', (scheme) => {
  it.each(['info', 'warn', 'danger'] as BannerTone[])(
    'tints the background with the theme colour of the %s tone',
    async (tone) => {
      await renderScreen(<Banner tone={tone} text="Offline" />, { scheme });

      expect(screen.getByTestId(primitiveIds.banner)).toHaveStyle({
        backgroundColor: tint(themes[scheme].colors[tone], 0.14),
        borderColor: 'transparent',
      });
      expect(screen.queryByTestId(actionId(primitiveIds.banner))).toBeNull();
    },
  );

  it('calls the action, under the testID given', async () => {
    const onPress = jest.fn();
    await renderScreen(
      <Banner
        tone="danger"
        text="Not sent"
        action={{ label: 'Retry', onPress }}
        testID="send-failed"
      />,
      { scheme },
    );

    await fireEvent.press(screen.getByTestId(actionId('send-failed')));

    expect(onPress).toHaveBeenCalledTimes(1);
    expect(screen.getByTestId(actionId('send-failed'))).toHaveStyle({ minHeight: 44 });
  });
});
