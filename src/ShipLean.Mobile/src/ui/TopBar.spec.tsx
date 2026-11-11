import { fireEvent, screen } from '@testing-library/react-native';
import { renderScreen } from '../test-support/renderScreen';
import { TopBar, topBarBackId, topBarSubId } from './TopBar';

describe.each(['light', 'dark'] as const)('TopBar (%s)', (scheme) => {
  test('render_WhenSubGiven_ShouldShowItUnderTheTitle', async () => {
    await renderScreen(<TopBar title="Sign in" sub="Step 1 of 3" />, { scheme });

    expect(screen.getByTestId(topBarSubId)).toHaveTextContent('Step 1 of 3');
  });

  test('render_WhenNoSubOrBack_ShouldShowNeither', async () => {
    await renderScreen(<TopBar title="Sign in" />, { scheme });

    expect(screen.queryByTestId(topBarSubId)).toBeNull();
    expect(screen.queryByTestId(topBarBackId)).toBeNull();
  });

  test('render_WhenOnBackGiven_ShouldShowBackThatCallsIt', async () => {
    const onBack = jest.fn();
    await renderScreen(<TopBar title="Sign in" onBack={onBack} />, { scheme });

    await fireEvent.press(screen.getByTestId(topBarBackId));

    expect(onBack).toHaveBeenCalledTimes(1);
  });
});
