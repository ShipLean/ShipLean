import { act, screen } from '@testing-library/react-native';
import { router } from 'expo-router';
import { links } from '../links';
import { screenIds, screenSetup } from '../screens/testIds';
import { renderApp } from '../test-support/renderApp';

type AnyLink = { pathname: string; params: Record<string, string> };

async function open(link: AnyLink) {
  await act(async () => router.push(link as Parameters<typeof router.push>[0]));
}

describe('routes', () => {
  it('opens each stack and settings route on its root id', async () => {
    const routes: [AnyLink, string][] = [
      [links.inbox('SHIP'), screenIds.inbox],
      [links.inbox('SHIP', 'setup'), screenIds.inbox],
      [
        links.backlog('SHIP', { level: 'features', epic: 'SHIP-3', feature: 'SHIP-12' }),
        screenIds.backlog,
      ],
      [links.features('SHIP'), screenIds.features],
      [links.retro('SHIP'), screenIds.retro],
      [links.item('SHIP', 'SHIP-141'), screenIds.workItem],
      [links.featureBoard('SHIP', 'SHIP-12'), screenIds.featureBoard],
      [links.digest('SHIP'), screenIds.digest],
      [links.settingsVoice(), screenIds.settingsVoice],
      [links.licences(), screenIds.licences],
      [links.setup('models'), screenSetup('models')],
      [links.setup('microphone'), screenSetup('microphone')],
      [links.setup('downloading'), screenSetup('downloading')],
    ];
    await renderApp();
    expect(await screen.findByTestId(screenIds.projects)).toBeOnTheScreen();

    for (const [link, root] of routes) {
      await open(link);
      expect(await screen.findByTestId(root)).toBeOnTheScreen();
      await act(async () => router.dismissTo(links.projects()));
      expect(screen.queryByTestId(root)).toBeNull();
    }
  });

  it('keeps the routes behind sign-in', async () => {
    await renderApp({ signedIn: false });
    expect(await screen.findByTestId(screenIds.signIn)).toBeOnTheScreen();

    await open(links.inbox('SHIP'));

    expect(await screen.findByTestId(screenIds.signIn)).toBeOnTheScreen();
    expect(screen.queryByTestId(screenIds.inbox)).toBeNull();
  });
});
