import { fireEvent, screen } from '@testing-library/react-native';
import { openBrowserAsync } from 'expo-web-browser';
import { TEST_ORIGIN } from '../test-support/builders';
import { renderApp } from '../test-support/renderApp';
import { projectsIds } from './projects/testIds';
import { accountIds } from './account/testIds';
import { screenIds } from './testIds';

test('shows name, email, host and version', async () => {
  await renderApp({ url: '/account' });

  expect(await screen.findByTestId(screenIds.account)).toBeOnTheScreen();
  expect(await screen.findByText('Joel Dickson')).toBeOnTheScreen();
  expect(screen.getByTestId(accountIds.email)).toHaveTextContent(/joel@shiplean.local/);
  expect(screen.getByTestId(accountIds.host)).toHaveTextContent(/^shiplean.test/);
  expect(await screen.findByText('1.4.0+7f26ef0a')).toBeOnTheScreen();
});

test('Profile row opens the server profile', async () => {
  await renderApp({ url: '/account' });

  await fireEvent.press(await screen.findByTestId(accountIds.profile));

  expect(openBrowserAsync).toHaveBeenCalledWith(`${TEST_ORIGIN}/profile`);
});

test('the avatar on Projects opens the sheet', async () => {
  await renderApp();

  await fireEvent.press(await screen.findByTestId(projectsIds.avatar));

  expect(await screen.findByTestId(screenIds.account)).toBeOnTheScreen();
});
