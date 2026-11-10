import { act, fireEvent, render } from '@testing-library/react-native';
import { Pressable, Text } from 'react-native';
import { RetroSelectionProvider, useLastTouchedPoint, useShownRetroSprint } from './lastTouched';

function Probe({ sprintId }: { sprintId: string | null }) {
  useShownRetroSprint(sprintId);
  const { pointId, touch, clear } = useLastTouchedPoint();
  return (
    <>
      <Text testID="point">{pointId ?? 'none'}</Text>
      <Pressable testID="touch" onPress={() => touch('pt-1')} />
      <Pressable testID="clear" onPress={clear} />
    </>
  );
}

const tree = (sprintId: string | null) => (
  <RetroSelectionProvider>
    <Probe sprintId={sprintId} />
  </RetroSelectionProvider>
);

describe('the last touched point', () => {
  it('remembers the last touched point and clears it when the sprint changes', async () => {
    const screen = await render(tree('s-25'));
    expect(screen.getByTestId('point').props.children).toBe('none');

    await fireEvent.press(screen.getByTestId('touch'));
    expect(screen.getByTestId('point').props.children).toBe('pt-1');

    await act(async () => screen.rerender(tree('s-25')));
    expect(screen.getByTestId('point').props.children).toBe('pt-1');

    await fireEvent.press(screen.getByTestId('clear'));
    expect(screen.getByTestId('point').props.children).toBe('none');

    await fireEvent.press(screen.getByTestId('touch'));
    await act(async () => screen.rerender(tree('s-24')));
    expect(screen.getByTestId('point').props.children).toBe('none');
  });
});
