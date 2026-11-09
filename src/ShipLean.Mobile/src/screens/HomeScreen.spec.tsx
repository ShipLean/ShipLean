import { render, screen } from '@testing-library/react-native';
import { HomeScreen } from './HomeScreen';

test('HomeScreen_WhenRendered_ShouldShowTheProductName', async () => {
  await render(<HomeScreen />);

  expect(await screen.findByText('ShipLean')).toBeOnTheScreen();
});
