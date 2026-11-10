import type { FeatureDigestDto } from '@shiplean/core/api/featuresOverview';
import { act, fireEvent, screen, waitFor, within } from '@testing-library/react-native';
import { UNDO } from '../../actions/undo';
import { links } from '../../links';
import { DigestScreen } from '../../screens/DigestScreen';
import { screenIds } from '../../screens/testIds';
import { aPending } from '../../test-support/actions';
import { aProject } from '../../test-support/builders';
import { aDigest, aDigestItem, aDigestLine, featuresApi } from '../../test-support/features';
import { apiError, type ApiTable } from '../../test-support/fakeApi';
import { renderWithRunner } from '../../test-support/renderWithRunner';
import type { Scheme } from '../../theme';
import { primitiveIds, uiIds } from '../../ui/testIds';
import { FeaturesActionsProvider } from '../FeaturesActions';
import {
  featureDigestIds,
  featureDigestItemId,
  featureDigestItemTypeId,
  featureDigestLineAgentTagId,
  featureDigestLineAtId,
  featureDigestLineKindId,
  featureDigestLineTextId,
  featureDigestLineWhoId,
} from '../testIds';

const mockPush = jest.fn();
const mockDismissTo = jest.fn();
jest.mock('expo-router', () => ({
  ...jest.requireActual('expo-router'),
  useRouter: () => ({ push: mockPush, dismissTo: mockDismissTo }),
}));

const PROJECT = aProject();
const project = `/api/projects/${PROJECT.id}`;
const DIGEST = `GET ${project}/features/digest`;

interface Opts {
  digest?: FeatureDigestDto;
  table?: ApiTable;
  scheme?: Scheme;
}

async function renderDigest({ digest, table = {}, scheme }: Opts = {}) {
  const api = featuresApi(table, { projectId: PROJECT.id, digest });
  const result = await renderWithRunner(
    <FeaturesActionsProvider>
      <DigestScreen projectKey="SHIP" />
    </FeaturesActionsProvider>,
    { scheme },
  );
  return { api, ...result };
}

const text = (id: string) => screen.getByTestId(id);
const order = () =>
  screen
    .getAllByTestId(/^feature-digest-item-(?!type)[A-Z]+-\d+$/)
    .map((n) => n.props.testID.slice('feature-digest-item-'.length));

const SEEN_ACTION = {
  area: 'features',
  kind: 'markDigestSeen',
  itemId: null,
  key: null,
  projectId: PROJECT.id,
};

// A scroll to the end of the list, as the user's thumb would.
const scrollToEnd = () =>
  fireEvent.scroll(screen.getByTestId(featureDigestIds.scroll), {
    nativeEvent: {
      contentOffset: { y: 900 },
      contentSize: { height: 1000, width: 390 },
      layoutMeasurement: { height: 700, width: 390 },
    },
  });

beforeEach(() => {
  mockPush.mockClear();
  mockDismissTo.mockClear();
});

describe.each(['light', 'dark'] as const)('Features digest (%s)', (scheme) => {
  it('renders the digest state: Needs you first, then each changed item with its lines', async () => {
    await renderDigest({ scheme });

    expect(await screen.findByTestId(featureDigestItemId('SHIP-906'))).toBeOnTheScreen();
    expect(screen.getByTestId(screenIds.digest)).toBeOnTheScreen();
    expect(text(uiIds.topBarTitle)).toHaveTextContent('What changed');
    expect(text(featureDigestIds.since)).toHaveTextContent('Mon 09:12');
    expect(text(featureDigestIds.needsYou)).toHaveTextContent('Needs you');
    expect(text(featureDigestIds.changed)).toHaveTextContent('Changed');
    expect(order()).toEqual(['SHIP-906', 'SHIP-907', 'SHIP-910']);

    const sections = screen
      .getAllByTestId(/^feature-digest-(needs-you|changed|item-SHIP-\d+)$/)
      .map((n) => n.props.testID);
    expect(sections).toEqual([
      featureDigestIds.needsYou,
      featureDigestItemId('SHIP-906'),
      featureDigestIds.changed,
      featureDigestItemId('SHIP-907'),
      featureDigestItemId('SHIP-910'),
    ]);

    expect(text(featureDigestItemTypeId('SHIP-907'))).toHaveTextContent('feature');
    expect(text(featureDigestItemTypeId('SHIP-910'))).toHaveTextContent('story');
    const item = within(text(featureDigestItemId('SHIP-907')));
    expect(item.getByText('Mobile app foundations')).toBeOnTheScreen();
    expect(text(featureDigestLineTextId('SHIP-907', 0))).toHaveTextContent('New to Active');
    expect(text(featureDigestLineKindId('SHIP-907', 0)).props.accessibilityLabel).toBe('State');
    expect(text(featureDigestLineTextId('SHIP-907', 1))).toHaveTextContent('Health set to At risk');
    expect(text(featureDigestLineKindId('SHIP-907', 1)).props.accessibilityLabel).toBe('Health');
    expect(text(featureDigestLineKindId('SHIP-906', 0)).props.accessibilityLabel).toBe('Question');
    expect(text(featureDigestLineAtId('SHIP-907', 0))).toHaveTextContent('Sat 21:05');
    expect(screen.getAllByText('Mark as seen')).toHaveLength(2);
  });

  it('opens the item when its title is tapped', async () => {
    await renderDigest({ scheme });

    const item = within(await screen.findByTestId(featureDigestItemId('SHIP-910')));
    await fireEvent.press(item.getByLabelText('Open SHIP-910'));

    expect(mockPush).toHaveBeenCalledWith(links.item('SHIP', 'SHIP-910'));
  });

  it('marks agent lines with the Agent tag and shows the server labels', async () => {
    const digest = aDigest({
      needsYou: [],
      changed: [
        aDigestItem({
          key: 'SHIP-920',
          lines: [
            aDigestLine('agentWork', {
              text: 'Closed 3 tasks',
              agentName: 'Claude Code',
              atLabel: '3h ago',
            }),
            aDigestLine('comment', {
              text: 'Added a note',
              by: {
                id: 'u1',
                name: 'Priya Shah',
                initials: 'PS',
                avatarColour: '#b4527a',
                photoUrl: null,
              },
              atLabel: 'yesterday',
            }),
          ],
        }),
      ],
    });
    await renderDigest({ digest, scheme });

    await screen.findByTestId(featureDigestItemId('SHIP-920'));
    expect(text(featureDigestLineWhoId('SHIP-920', 0))).toHaveTextContent('Claude Code');
    expect(text(featureDigestLineAgentTagId('SHIP-920', 0))).toHaveTextContent('Agent');
    expect(text(featureDigestLineAtId('SHIP-920', 0))).toHaveTextContent('3h ago');
    expect(text(featureDigestLineTextId('SHIP-920', 0))).toHaveTextContent('Closed 3 tasks');
    expect(text(featureDigestLineWhoId('SHIP-920', 1))).toHaveTextContent('Priya Shah');
    expect(screen.queryByTestId(featureDigestLineAgentTagId('SHIP-920', 1))).toBeNull();
    expect(text(featureDigestLineAtId('SHIP-920', 1))).toHaveTextContent('yesterday');
  });

  it('loads the next page with the cursor when more is true', async () => {
    const first = aDigest({ cursor: 'page-2', more: true });
    const second = aDigest({
      needsYou: [],
      changed: [aDigestItem({ key: 'SHIP-930', title: 'Second page item' })],
      cursor: null,
      more: false,
    });
    const { api } = await renderDigest({
      digest: first,
      table: {
        [DIGEST]: (request) => (request.url.includes('cursor=page-2') ? second : first),
      },
      scheme,
    });

    await screen.findByTestId(featureDigestItemId('SHIP-906'));
    expect(screen.queryByTestId(featureDigestItemId('SHIP-930'))).toBeNull();
    expect(api.callsTo(DIGEST)).toHaveLength(1);
    expect(api.callsTo(DIGEST)[0].url).not.toContain('cursor');

    await scrollToEnd();

    expect(await screen.findByTestId(featureDigestItemId('SHIP-930'))).toBeOnTheScreen();
    const urls = api.callsTo(DIGEST).map((c) => c.url);
    expect(urls).toHaveLength(2);
    expect(urls[1]).toContain('cursor=page-2');
    // A page appends: the first page stays, and each section has one heading.
    expect(order()).toEqual(['SHIP-906', 'SHIP-907', 'SHIP-910', 'SHIP-930']);
    expect(screen.getAllByTestId(featureDigestIds.changed)).toHaveLength(1);
    expect(screen.queryByTestId(featureDigestIds.loadingMore)).toBeNull();

    await scrollToEnd();
    expect(api.callsTo(DIGEST)).toHaveLength(2);
  });

  it('shows Loading more while the next page is read', async () => {
    const first = aDigest({ cursor: 'page-2', more: true });
    const { api } = await renderDigest({
      digest: first,
      table: {
        [DIGEST]: (request) =>
          request.url.includes('cursor=') ? aDigest({ needsYou: [], changed: [] }) : first,
      },
      scheme,
    });
    await screen.findByTestId(featureDigestItemId('SHIP-906'));
    api.hang(DIGEST);

    await scrollToEnd();

    expect(await screen.findByTestId(featureDigestIds.loadingMore)).toHaveTextContent(
      'Loading more…',
    );
    await act(async () => api.release(DIGEST));
    await waitFor(() => expect(screen.queryByTestId(featureDigestIds.loadingMore)).toBeNull());
  });

  it('dispatches markDigestSeen once and goes back to the Features tab', async () => {
    const { runner } = await renderDigest({ scheme });
    await screen.findByTestId(featureDigestItemId('SHIP-906'));
    expect(runner.dispatched).toHaveLength(0);

    await fireEvent.press(text(featureDigestIds.seenBottom));
    await fireEvent.press(text(featureDigestIds.seenBottom));
    await fireEvent.press(text(featureDigestIds.seenTop));

    expect(runner.dispatched).toHaveLength(1);
    expect(runner.dispatched[0].batch).toEqual([SEEN_ACTION]);
    expect(runner.dispatched[0].opts.undo).toEqual(UNDO.none);
    expect(mockDismissTo).toHaveBeenCalledTimes(1);
    expect(mockDismissTo).toHaveBeenCalledWith(links.features('SHIP'));
  });

  it('reads the digest and overview again once the seen has been sent', async () => {
    const { api, runner } = await renderDigest({ scheme });
    await screen.findByTestId(featureDigestItemId('SHIP-906'));
    expect(api.callsTo(DIGEST)).toHaveLength(1);

    await fireEvent.press(text(featureDigestIds.seenTop));
    expect(api.callsTo(DIGEST)).toHaveLength(1);
    await act(async () =>
      runner.setPending([
        aPending({ batchId: 'batch-1', state: 'sent', action: SEEN_ACTION as never }),
      ]),
    );

    await waitFor(() => expect(api.callsTo(DIGEST)).toHaveLength(2));
  });

  it('shows Nothing changed with Mark as seen', async () => {
    const digest = aDigest({ needsYou: [], changed: [], sinceLabel: 'since yesterday' });
    const { runner } = await renderDigest({ digest, scheme });

    expect(await screen.findByTestId(featureDigestIds.empty)).toHaveTextContent(
      'Nothing changed since yesterday.',
    );
    expect(screen.queryByTestId(featureDigestIds.needsYou)).toBeNull();
    expect(screen.queryByTestId(featureDigestIds.changed)).toBeNull();
    await fireEvent.press(text(featureDigestIds.seenBottom));
    expect(runner.dispatched).toHaveLength(1);
  });

  it('shows the error state with Retry when the digest fails to load', async () => {
    const api = featuresApi({}, { projectId: PROJECT.id });
    api.failNext(DIGEST, apiError(500, 'server_error'));
    await renderWithRunner(
      <FeaturesActionsProvider>
        <DigestScreen projectKey="SHIP" />
      </FeaturesActionsProvider>,
      { scheme },
    );

    const error = await screen.findByTestId(uiIds.errorState);
    expect(within(error).getByText("Couldn't load what changed.")).toBeOnTheScreen();
    expect(screen.queryByTestId(featureDigestIds.seenBottom)).toBeNull();

    await fireEvent.press(text(uiIds.errorStateAction));

    expect(await screen.findByTestId(featureDigestItemId('SHIP-906'))).toBeOnTheScreen();
    expect(screen.queryByTestId(uiIds.errorState)).toBeNull();
  });

  it('shows the error state and a Retry row for a failed page', async () => {
    const first = aDigest({ cursor: 'page-2', more: true });
    const second = aDigest({
      needsYou: [],
      changed: [aDigestItem({ key: 'SHIP-930', title: 'Second page item' })],
    });
    const { api } = await renderDigest({
      digest: first,
      table: {
        [DIGEST]: (request) => (request.url.includes('cursor=page-2') ? second : first),
      },
      scheme,
    });
    await screen.findByTestId(featureDigestItemId('SHIP-906'));
    api.failNext(DIGEST, apiError(500, 'server_error'));

    await scrollToEnd();

    expect(await screen.findByTestId(featureDigestIds.pageRetry)).toBeOnTheScreen();
    // What loaded stays, and the first-page error state is not shown.
    expect(screen.queryByTestId(uiIds.errorState)).toBeNull();
    expect(order()).toEqual(['SHIP-906', 'SHIP-907', 'SHIP-910']);

    await fireEvent.press(text(featureDigestIds.pageRetry));

    expect(await screen.findByTestId(featureDigestItemId('SHIP-930'))).toBeOnTheScreen();
    expect(screen.queryByTestId(featureDigestIds.pageRetry)).toBeNull();
  });

  it('offers no Read it out', async () => {
    await renderDigest({ scheme });
    await screen.findByTestId(featureDigestItemId('SHIP-906'));

    expect(screen.queryByText(/read it out/i)).toBeNull();
    expect(screen.queryByLabelText(/read it out/i)).toBeNull();
    expect(screen.queryByRole('button', { name: /read it out/i })).toBeNull();
  });

  it('shows a skeleton and sends no read with an empty project id on a cold open', async () => {
    const api = featuresApi({}, { projectId: PROJECT.id });
    api.hang('GET /api/projects');
    await renderWithRunner(
      <FeaturesActionsProvider>
        <DigestScreen projectKey="SHIP" />
      </FeaturesActionsProvider>,
      { scheme },
    );

    expect(await screen.findByTestId(primitiveIds.skeleton)).toBeOnTheScreen();
    expect(api.calls.filter((c) => c.path.includes('/projects//'))).toHaveLength(0);
    expect(api.callsTo(DIGEST)).toHaveLength(0);

    await act(async () => api.release('GET /api/projects'));

    expect(await screen.findByTestId(featureDigestItemId('SHIP-906'))).toBeOnTheScreen();
    expect(api.calls.filter((c) => c.path.includes('/projects//'))).toHaveLength(0);
  });
});
