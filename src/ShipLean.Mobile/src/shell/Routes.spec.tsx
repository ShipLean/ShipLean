import { act, screen } from '@testing-library/react-native';
import { router } from 'expo-router';
import { links } from '../links';
import { screenIds, screenSetup } from '../screens/testIds';
import { renderApp } from '../test-support/renderApp';

type AnyLink = { pathname: string; params: Record<string, string> };

async function open(link: AnyLink) {
  await act(async () => router.push(link as Parameters<typeof router.push>[0]));
}

function pathOf(link: AnyLink) {
  return link.pathname.replace(/\[(\w+)\]/g, (_m, name: string) => link.params[name] ?? '');
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

  it('opens a step that does not exist on the projects list', async () => {
    await renderApp();
    expect(await screen.findByTestId(screenIds.projects)).toBeOnTheScreen();

    await open({ pathname: links.setup('models').pathname, params: { step: 'nope' } });

    expect(await screen.findByTestId(screenIds.projects)).toBeOnTheScreen();
  });

  it.each([
    ['an item', links.item('SHIP', 'SHIP-141'), screenIds.workItem],
    ['a Feature board', links.featureBoard('SHIP', 'SHIP-12'), screenIds.featureBoard],
    ['the digest', links.digest('SHIP'), screenIds.digest],
  ])('opens with the tabs underneath when a cold link opens %s', async (_name, link, root) => {
    await renderApp({ url: pathOf(link) });
    expect(await screen.findByTestId(root)).toBeOnTheScreen();

    await act(async () => router.back());

    expect(screen.queryByTestId(root)).toBeNull();
    expect(screen.getByTestId(screenIds.inbox)).toBeOnTheScreen();
  });

  it('keeps the routes behind sign-in', async () => {
    await renderApp({ signedIn: false });
    expect(await screen.findByTestId(screenIds.signIn)).toBeOnTheScreen();

    await open(links.inbox('SHIP'));

    expect(await screen.findByTestId(screenIds.signIn)).toBeOnTheScreen();
    expect(screen.queryByTestId(screenIds.inbox)).toBeNull();
  });
});
