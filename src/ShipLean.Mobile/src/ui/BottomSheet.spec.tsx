import { fireEvent, screen } from '@testing-library/react-native';
import { Text } from 'react-native';
import { renderScreen } from '../test-support/renderScreen';
import { themes } from '../theme';
import { BottomSheet } from './BottomSheet';
import { primitiveIds, sheetTitleId } from './testIds';

describe.each(['light', 'dark'] as const)('BottomSheet (%s)', (scheme) => {
  it('closes when the backdrop is pressed', async () => {
    const onClose = jest.fn();
    await renderScreen(
      <BottomSheet title="Reply" open onClose={onClose} testID="reply-sheet">
        <Text testID="body">body</Text>
      </BottomSheet>,
      { scheme },
    );

    await fireEvent.press(screen.getByTestId(primitiveIds.sheetBackdrop));

    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('shows its title id, its children and the theme surface when open', async () => {
    await renderScreen(
      <BottomSheet title="Reply" open onClose={() => {}} testID="reply-sheet">
        <Text testID="body">body</Text>
      </BottomSheet>,
      { scheme },
    );

    expect(screen.getByTestId(sheetTitleId('reply-sheet'))).toBeOnTheScreen();
    expect(screen.getByTestId('reply-sheet')).toContainElement(screen.getByTestId('body'));
    expect(screen.getByTestId('reply-sheet')).toHaveStyle({
      backgroundColor: themes[scheme].colors.surface,
    });
  });

  it('renders nothing when closed', async () => {
    await renderScreen(
      <BottomSheet title="Reply" open={false} onClose={() => {}} testID="reply-sheet">
        <Text testID="body">body</Text>
      </BottomSheet>,
      { scheme },
    );

    expect(screen.queryByTestId('reply-sheet')).toBeNull();
    expect(screen.queryByTestId(primitiveIds.sheetBackdrop)).toBeNull();
  });
});
