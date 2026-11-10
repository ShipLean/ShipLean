import { useRouter } from 'expo-router';
import { NavigationContext } from 'expo-router/react-navigation';
import { useCallback, useContext, useEffect, useRef, useState } from 'react';
import { AppState, Pressable, RefreshControl, ScrollView, Text, View } from 'react-native';
import { AnsweredList } from '../inbox/AnsweredList';
import { useInboxActions } from '../inbox/InboxActions';
import { InboxRow } from '../inbox/InboxRow';
import {
  ANSWERED_SUBTITLE,
  emptyKind,
  inboxCounts,
  visibleItems,
  waitingSubtitle,
} from '../inbox/model';
import { OfflineBanner } from '../inbox/OfflineBanner';
import { useAnsweredQuery, useInboxQuery } from '../inbox/queries';
import { inboxIds } from '../inbox/testIds';
import { links } from '../links';
import { useProject } from '../shell/useProject';
import { useTheme } from '../theme';
import { EmptyState } from '../ui/EmptyState';
import { ErrorState } from '../ui/ErrorState';
import { Screen } from '../ui/Screen';
import { Segmented } from '../ui/Segmented';
import { Skeleton } from '../ui/Skeleton';
import { TabRootBar } from '../ui/TabRootBar';
import { screenIds } from './testIds';

export interface InboxScreenProps {
  projectKey: string;
  // 'setup' when the setup's first command opened the Inbox with its answer.
  from?: 'setup';
}

type Segment = 'waiting' | 'answered';

const KINDS: { title: string; body: string }[] = [
  {
    title: 'Decisions to confirm',
    body: 'Options with a recommended answer: approve, change or discuss.',
  },
  { title: 'Questions', body: 'Answer with a tap, by typing, or by voice.' },
  {
    title: 'Mentions and status checks',
    body: 'A note that @mentions you, or your Feature’s status line once it’s 3 days old.',
  },
  {
    title: 'Answers go on the work item',
    body: 'Agents never wait; the next one to pick it up reads it.',
  },
];

// The Inbox tab: everything waiting on you in the project, and what you answered. The project id is read from the projects
// list, so nothing is requested until it is known.
export function InboxScreen({ projectKey }: InboxScreenProps) {
  const router = useRouter();
  const { project, isLoading, error } = useProject(projectKey);
  if (project !== undefined) return <InboxBody projectKey={projectKey} projectId={project.id} />;
  return (
    <Screen testID={screenIds.inbox}>
      <TabRootBar
        projectKey={projectKey}
        title="Inbox"
        onTilePress={() => router.push(links.projects())}
      />
      {isLoading ? (
        <Skeleton rows={5} />
      ) : (
        <ErrorState
          title="Couldn't open this project."
          body={error?.message ?? `You aren't in a project called ${projectKey}.`}
        />
      )}
    </Screen>
  );
}

// Refetches when the tab is focused again (tabs stay mounted) and when the app returns to the foreground. Outside a
// navigator there is no focus event, and mounting is the open.
function useRefreshOnReturn(refresh: () => void) {
  const nav = useContext(NavigationContext);
  const latest = useRef(refresh);
  useEffect(() => {
    latest.current = refresh;
  });
  useEffect(() => {
    const app = AppState.addEventListener('change', (status) => {
      if (status === 'active') latest.current();
    });
    const off = nav?.addListener('focus', () => latest.current());
    return () => {
      app.remove();
      off?.();
    };
  }, [nav]);
}

function InboxBody({ projectKey, projectId }: { projectKey: string; projectId: string }) {
  const router = useRouter();
  const theme = useTheme();
  const { colors } = theme;
  const inbox = useInboxQuery(projectId);
  const answered = useAnsweredQuery(projectId);
  const { pending } = useInboxActions();
  const [segment, setSegment] = useState<Segment>('waiting');
  const [expanded, setExpanded] = useState<Record<string, boolean>>({});
  const [pulled, setPulled] = useState(false);

  const { refetch: refetchInbox } = inbox;
  const { refetch: refetchAnswered } = answered;
  const refresh = useCallback(async () => {
    await Promise.allSettled([
      refetchInbox({ cancelRefetch: false }),
      refetchAnswered({ cancelRefetch: false }),
    ]);
  }, [refetchInbox, refetchAnswered]);
  useRefreshOnReturn(() => void refresh());

  const data = inbox.data;
  const counts = data === undefined ? undefined : inboxCounts(data, pending);
  const visible = data === undefined ? [] : visibleItems(data.items, pending);
  const subtitle =
    segment === 'answered'
      ? ANSWERED_SUBTITLE
      : data === undefined || counts === undefined
        ? undefined
        : waitingSubtitle(counts, data.everWaited);

  const waitingList = () => {
    if (data === undefined)
      return inbox.isError ? (
        <ErrorState
          title="Couldn't load your Inbox."
          body={inbox.error.message}
          action={{
            label: 'Retry',
            busy: inbox.isFetching,
            onPress: () => void refetchInbox(),
          }}
        />
      ) : (
        <Skeleton rows={5} />
      );
    const empty = emptyKind(data, visible);
    if (empty === 'clear')
      return (
        <View testID={inboxIds.empty}>
          <EmptyState
            title="Nothing waits on you"
            body="When an agent or a person needs a decision or an answer from you, it lands here. Agents carry on with other work meanwhile."
          />
          <Pressable
            testID={inboxIds.today}
            accessibilityRole="button"
            onPress={() => setSegment('answered')}
            style={{
              margin: theme.space[4],
              padding: theme.space[3],
              borderRadius: theme.radius.md,
              backgroundColor: colors.surface,
              borderWidth: 1,
              borderColor: colors.border,
            }}
          >
            <Text style={{ color: colors.text, fontWeight: theme.font.weight.semibold }}>
              {`You answered ${answered.data?.answeredToday ?? 0} today`}
            </Text>
            <Text style={{ color: colors.muted, fontSize: theme.font.size.sm }}>
              {`Agents have picked up ${answered.data?.pickedUpToday ?? 0} of them`}
            </Text>
          </Pressable>
        </View>
      );
    if (empty === 'new')
      return (
        <View testID={inboxIds.emptyNew}>
          <EmptyState
            title="Nothing here yet"
            body="This is where people and agents ask you for things."
          />
          <View
            testID={inboxIds.kinds}
            style={{
              margin: theme.space[4],
              borderRadius: theme.radius.md,
              backgroundColor: colors.surface,
              borderWidth: 1,
              borderColor: colors.border,
            }}
          >
            {KINDS.map((k) => (
              <View key={k.title} style={{ padding: theme.space[3] }}>
                <Text style={{ color: colors.text, fontWeight: theme.font.weight.semibold }}>
                  {k.title}
                </Text>
                <Text style={{ color: colors.muted, fontSize: theme.font.size.sm }}>{k.body}</Text>
              </View>
            ))}
          </View>
        </View>
      );
    return (
      <>
        {visible.map((item) => (
          <InboxRow
            key={item.id}
            projectId={projectId}
            item={item}
            pending={pending.filter((p) => p.action.itemId === item.id)}
            indented={item.underKey !== null}
            expanded={expanded[item.id] === true}
            onToggleExpanded={() => setExpanded((e) => ({ ...e, [item.id]: !e[item.id] }))}
            onOpenItem={() => router.push(links.item(projectKey, item.key))}
          />
        ))}
      </>
    );
  };

  return (
    <Screen testID={screenIds.inbox}>
      <TabRootBar
        projectKey={projectKey}
        title="Inbox"
        subtitle={subtitle}
        onTilePress={() => router.push(links.projects())}
      />
      <View
        style={{
          flexDirection: 'row',
          padding: theme.space[2],
          backgroundColor: colors.surface,
          borderBottomWidth: 1,
          borderBottomColor: colors.border,
        }}
      >
        <Segmented
          testID={inboxIds.segment}
          value={segment}
          onChange={setSegment}
          options={[
            {
              value: 'waiting',
              label: 'Waiting on you',
              count: counts !== undefined && counts.asks > 0 ? counts.asks : undefined,
            },
            { value: 'answered', label: 'Answered' },
          ]}
        />
      </View>
      {data === undefined ? null : <OfflineBanner loadedAtLabel={data.loadedAtLabel} />}
      <ScrollView
        testID={inboxIds.list}
        refreshControl={
          <RefreshControl
            refreshing={pulled && inbox.fetchStatus === 'fetching'}
            onRefresh={() => {
              setPulled(true);
              void refresh().finally(() => setPulled(false));
            }}
          />
        }
      >
        {segment === 'answered' ? <AnsweredList projectId={projectId} /> : waitingList()}
      </ScrollView>
    </Screen>
  );
}
