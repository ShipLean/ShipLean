import { screen } from '@testing-library/react-native';
import { renderApp } from '../test-support/renderApp';
import { renderScreen } from '../test-support/renderScreen';
import { TabRootBar } from '../ui/TabRootBar';
import { primitiveIds } from '../ui/testIds';
import { projectRowId } from '../screens/projects/testIds';
import { downloadBannerIds } from '../voice/DownloadBanner.testIds';

const mockMounted = jest.fn();
const mockGate = {
  useGate: () => {
    const { useEffect } = jest.requireActual<typeof import('react')>('react');
    useEffect(() => {
      mockMounted();
    }, []);
  },
};
jest.mock('./setupGate', () => ({
  // Counts component instances that call the hook, so a call per row would show up.
  useSetupGate: () => mockGate.useGate(),
}));

// The testIDs in the order the tree renders them.
function idsInOrder(node: ReturnType<typeof screen.toJSON>): string[] {
  if (node === null) return [];
  if (Array.isArray(node)) return node.flatMap((n) => idsInOrder(n));
  const own = typeof node.props.testID === 'string' ? [node.props.testID] : [];
  return [
    ...own,
    ...(node.children ?? []).flatMap((c) => (typeof c === 'string' ? [] : idsInOrder(c))),
  ];
}

beforeEach(() => mockMounted.mockClear());

describe('setup mounts', () => {
  it('renders voice-download-banner under the tab root bar', async () => {
    await renderScreen(
      <TabRootBar projectKey="SHIP" title="Inbox" onTilePress={() => undefined} />,
    );

    expect(screen.getByTestId(primitiveIds.tabRootBar)).toBeOnTheScreen();
    expect(screen.getByTestId(downloadBannerIds.root)).toBeOnTheScreen();
    const ids = idsInOrder(screen.toJSON());
    expect(ids.indexOf(downloadBannerIds.root)).toBeGreaterThan(
      ids.indexOf(primitiveIds.tabRootBar),
    );
  });

  it('Projects calls useSetupGate once when it renders', async () => {
    await renderApp();
    await screen.findByTestId(projectRowId('SHIP'));
    expect(screen.getByTestId(projectRowId('OPS'))).toBeOnTheScreen();

    expect(mockMounted).toHaveBeenCalledTimes(1);
  });
});
