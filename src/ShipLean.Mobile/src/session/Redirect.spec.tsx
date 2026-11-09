import { screen } from '@testing-library/react-native';
import { screenIds } from '../screens/testIds';
import { renderApp } from '../test-support/renderApp';

// Android delivers the browser handoff's redirect as a deep link too, and expo-router reads shiplean://signed-in?…
// as the path /signed-in?…, which no route matches; app/+native-intent.ts keeps it off the router.
test('redirect_WhenTheAppOpensOnTheSignedInRedirectWhileSignedOut_ShouldStayOnSignIn', async () => {
  const app = await renderApp({ signedIn: false, url: '/signed-in?code=x&state=y' });

  expect(await screen.findByTestId(screenIds.signIn)).toBeOnTheScreen();
  expect(app.getPathname()).toBe('/sign-in');
});

test('redirect_WhenTheAppOpensOnTheRawRedirectUrlWhileSignedOut_ShouldStayOnSignIn', async () => {
  await renderApp({ signedIn: false, url: 'shiplean://signed-in?code=x&state=y' });

  expect(await screen.findByTestId(screenIds.signIn)).toBeOnTheScreen();
});
