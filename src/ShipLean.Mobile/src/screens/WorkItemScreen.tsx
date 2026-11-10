import { ApiRequestError } from '@shiplean/core/api/client';
import type { WorkItemDto } from '@shiplean/core/api/workItems';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { RefreshControl, ScrollView, Text, View } from 'react-native';
import { useInboxActions } from '../inbox/InboxActions';
import { useInboxSheets } from '../inbox/InboxSheets';
import { newStatusAsk, statusAsk, toInboxItem } from '../inbox/toInboxItem';
import { useProject } from '../shell/useProject';
import { useTheme } from '../theme';
import { Dock } from '../ui/Dock';
import { EmptyState } from '../ui/EmptyState';
import { ErrorState } from '../ui/ErrorState';
import { Screen } from '../ui/Screen';
import { Skeleton } from '../ui/Skeleton';
import { TopBar } from '../ui/TopBar';
import { useScreenVoice } from '../voice/VoiceProvider';
import { AcceptanceCriteria } from '../workItem/details/AcceptanceCriteria';
import { GoalDetails } from '../workItem/details/GoalDetails';
import { RefusedChanges } from '../workItem/header/RefusedChanges';
import { StatusLine } from '../workItem/header/StatusLine';
import { WorkItemHeader } from '../workItem/header/WorkItemHeader';
import { ItemActionsRow } from '../workItem/actionsRow/ItemActionsRow';
import { NeedsYouSection } from '../workItem/needs/NeedsYouSection';
import { CompactProgress } from '../workItem/progress/CompactProgress';
import { ProgressSection } from '../workItem/progress/ProgressSection';
import { StoryTasks } from '../workItem/progress/StoryTasks';
import {
  useInvalidateWorkItem,
  useItemAsksQuery,
  useMembersQuery,
  useTypeStatesQuery,
  useWorkItemQuery,
} from '../workItem/queries';
import { workItemScrollId } from '../workItem/testIds';
import { Timeline } from '../workItem/timeline/Timeline';
import { workItemVoiceSpec } from '../workItem/voiceSpec';
import { screenIds } from './testIds';

export interface WorkItemScreenProps {
  projectKey: string;
  itemKey: string;
}

function Back() {
  const router = useRouter();
  const theme = useTheme();
  if (!router.canGoBack()) return null;
  return (
    <Text
      accessibilityRole="button"
      onPress={() => router.back()}
      style={{ color: theme.colors.accent, fontSize: theme.font.size.md }}
    >
      Back
    </Text>
  );
}

export function WorkItemScreen({ projectKey, itemKey }: WorkItemScreenProps) {
  const { project, isLoading } = useProject(projectKey);
  return (
    <Screen testID={screenIds.workItem}>
      <TopBar title={itemKey} left={<Back />} />
      {project === undefined ? (
        isLoading ? (
          <Skeleton rows={4} />
        ) : (
          <EmptyState title={`${itemKey} isn't in this project.`} />
        )
      ) : (
        <WorkItemBody projectId={project.id} projectKey={projectKey} itemKey={itemKey} />
      )}
    </Screen>
  );
}

interface BodyProps {
  projectId: string;
  projectKey: string;
  itemKey: string;
}

function WorkItemBody({ projectId, projectKey, itemKey }: BodyProps) {
  const query = useWorkItemQuery(projectId, itemKey);
  if (query.data !== undefined) {
    return <Loaded item={query.data} projectId={projectId} projectKey={projectKey} />;
  }
  if (query.isError) {
    if (query.error instanceof ApiRequestError && query.error.status === 404) {
      return <EmptyState title={`${itemKey} isn't in this project.`} />;
    }
    return (
      <ErrorState
        title={`Couldn't load ${itemKey}.`}
        body={query.error.message}
        action={{ label: 'Retry', busy: query.isFetching, onPress: () => void query.refetch() }}
      />
    );
  }
  return <Skeleton rows={4} />;
}

interface LoadedProps {
  item: WorkItemDto;
  projectId: string;
  projectKey: string;
}

function Loaded({ item, projectId, projectKey }: LoadedProps) {
  const theme = useTheme();
  const invalidate = useInvalidateWorkItem();
  const [refreshing, setRefreshing] = useState(false);
  const actions = useInboxActions();
  const sheets = useInboxSheets();
  const asks = useItemAsksQuery(projectId, item.key, item.id);
  const states = useTypeStatesQuery(projectId, item.type.id);
  const members = useMembersQuery(projectId);

  useScreenVoice(
    workItemVoiceSpec({
      item,
      asks: asks.data ?? null,
      states: states.data ?? [],
      members: members.data ?? [],
    }),
  );

  const refresh = async () => {
    setRefreshing(true);
    try {
      await invalidate(projectId, item.key, item.id);
    } finally {
      setRefreshing(false);
    }
  };

  const feature = item.type.id === 'feature';
  const story = item.type.id === 'story';
  const ask = statusAsk(item) ?? newStatusAsk(item);

  return (
    <View style={{ flex: 1 }}>
      <RefusedChanges itemId={item.id} />
      <ScrollView
        testID={workItemScrollId}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refresh()} />}
        contentContainerStyle={{ paddingBottom: theme.space[5] }}
      >
        <WorkItemHeader item={item} projectKey={projectKey} />
        {feature ? <CompactProgress item={item} projectId={projectId} /> : null}
        <ItemActionsRow item={item} projectKey={projectKey} />
        {feature ? (
          <StatusLine
            item={item}
            onStillTrue={() =>
              actions.dispatch({
                area: 'inbox',
                projectId,
                itemId: item.id,
                key: item.key,
                kind: 'stillTrue',
                revision: item.revision,
              })
            }
            onNewStatus={() =>
              sheets.openAnswer({
                mode: 'status',
                item: toInboxItem(item, asks.data ?? null),
                ask,
                projectId,
              })
            }
          />
        ) : null}
        <NeedsYouSection item={item} projectKey={projectKey} />
        {feature ? <ProgressSection item={item} projectId={projectId} /> : null}
        {feature ? <GoalDetails item={item} /> : null}
        {story ? <StoryTasks item={item} projectId={projectId} /> : null}
        {story ? <AcceptanceCriteria item={item} /> : null}
        <Timeline item={item} />
      </ScrollView>
      <Dock itemId={item.id} itemKey={item.key} projectId={projectId} placeholder="Type a note" />
    </View>
  );
}
