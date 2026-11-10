import { ApiRequestError } from '@shiplean/core/api/client';
import type { FeatureBoardViewDto } from '@shiplean/core/api/views';
import { useRouter } from 'expo-router';
import { useMemo, useState } from 'react';
import { RefreshControl, ScrollView, Text, View } from 'react-native';
import { useInboxQuery } from '../../inbox/queries';
import { links } from '../../links';
import { useProject } from '../../shell/useProject';
import { useTheme } from '../../theme';
import { EmptyState } from '../../ui/EmptyState';
import { ErrorState } from '../../ui/ErrorState';
import { Skeleton } from '../../ui/Skeleton';
import { StatePill } from '../../ui/StatePill';
import { useScreenVoice } from '../../voice/VoiceProvider';
import { useWorkItemQuery } from '../../workItem/queries';
import { useFeatureMembers } from '../list/useFeatureMembers';
import { useFeatureBoardQuery, useFeatureStatesQuery, useFeaturesOverviewQuery } from '../queries';
import {
  featureBoardGroupCountId,
  featureBoardGroupId,
  featureBoardIds,
  featureBoardSectionEmptyId,
  featureBoardSectionId,
} from '../testIds';
import { featuresVoiceSpec } from '../voiceSpec';
import { BoardHeader } from './BoardHeader';
import { boardFigures, groupRows, sectionLayout, sectionTitle, type RowGroup } from './boardModel';
import { StoryRow } from './StoryRow';

export interface FeatureBoardViewProps {
  projectKey: string;
  featureKey: string;
}

// Resolves the project, then mounts the board with a real project id so no read is sent before it.
export function FeatureBoardView({ projectKey, featureKey }: FeatureBoardViewProps) {
  const { project, isLoading, error } = useProject(projectKey);
  if (project !== undefined) {
    return <FeatureBoard projectId={project.id} projectKey={projectKey} featureKey={featureKey} />;
  }
  if (isLoading && error === null) return <Skeleton rows={6} />;
  return <ErrorState title="Couldn't load the board." />;
}

interface FeatureBoardProps {
  projectId: string;
  projectKey: string;
  featureKey: string;
}

function FeatureBoard({ projectId, projectKey, featureKey }: FeatureBoardProps) {
  const boardQuery = useFeatureBoardQuery(projectId, featureKey);
  const itemQuery = useWorkItemQuery(projectId, featureKey);
  const overviewQuery = useFeaturesOverviewQuery(projectId);
  const inbox = useInboxQuery(projectId);
  const states = useFeatureStatesQuery(projectId);
  const members = useFeatureMembers(projectId);
  const board = boardQuery.data;

  const overviewRows = useMemo(
    () => overviewQuery.data?.groups.flatMap((g) => g.rows) ?? [],
    [overviewQuery.data],
  );
  const row = overviewRows.find((r) => r.key === featureKey);

  // F-V's commands leave the Feature's key out here: it is the focus. A Closed Feature has no row in the overview.
  useScreenVoice(
    featuresVoiceSpec({
      projectId,
      board: true,
      focus: row ?? null,
      rows: overviewRows,
      order: overviewRows,
      states: states.data ?? [],
      asks: inbox.data?.items ?? [],
      members: members.data ?? [],
      projectKey,
      filtering: false,
    }),
  );

  const [refreshing, setRefreshing] = useState(false);
  const refresh = async () => {
    setRefreshing(true);
    try {
      await Promise.all([
        boardQuery.refetch(),
        itemQuery.refetch(),
        overviewQuery.refetch(),
        inbox.refetch(),
        states.refetch(),
        members.refetch(),
      ]);
    } finally {
      setRefreshing(false);
    }
  };

  if (board === undefined) {
    if (!boardQuery.isError) return <Skeleton rows={6} />;
    if (boardQuery.error instanceof ApiRequestError && boardQuery.error.status === 404) {
      return <EmptyState title={`${featureKey} isn't in this project.`} />;
    }
    return (
      <ErrorState
        title="Couldn't load the board."
        action={{
          label: 'Retry',
          busy: boardQuery.isFetching,
          onPress: () => void boardQuery.refetch(),
        }}
      />
    );
  }

  return (
    <ScrollView
      testID={featureBoardIds.scroll}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refresh()} />}
    >
      <BoardHeader
        projectKey={projectKey}
        featureKey={featureKey}
        header={board.header}
        figures={boardFigures(board)}
        item={itemQuery.data}
        row={row}
        overviewSettled={overviewQuery.isSuccess || overviewQuery.isError}
      />
      <Stories view={board} projectKey={projectKey} />
    </ScrollView>
  );
}

interface StoriesProps {
  view: FeatureBoardViewDto;
  projectKey: string;
}

// The stories and bugs grouped by state. A Feature with Features under it shows one titled section each, depth-first.
function Stories({ view, projectKey }: StoriesProps) {
  const theme = useTheme();
  const router = useRouter();
  const sections = sectionLayout(view);
  if (view.rows.length === 0) {
    return <EmptyState title="No stories or bugs yet." />;
  }
  const open = (key: string) => router.push(links.item(projectKey, key));
  const groups = (scope: string, list: RowGroup[]) =>
    list.map((group) => (
      <View key={group.category} testID={featureBoardGroupId(scope, group.category)}>
        <View
          style={{
            flexDirection: 'row',
            alignItems: 'center',
            gap: theme.space[2],
            paddingTop: theme.space[4],
            paddingBottom: theme.space[2],
            paddingHorizontal: theme.space[4],
          }}
        >
          {group.folded ? (
            <Text
              testID={featureBoardGroupCountId(scope, group.category)}
              style={{ color: theme.colors.accent, fontSize: theme.font.size.sm }}
            >
              {group.label} · {group.rows.length}
            </Text>
          ) : (
            <>
              <StatePill
                state={{ id: group.category, name: group.label, category: group.category }}
              />
              <Text
                testID={featureBoardGroupCountId(scope, group.category)}
                style={{ color: theme.colors.muted, fontSize: theme.font.size.sm }}
              >
                {group.rows.length}
              </Text>
            </>
          )}
        </View>
        {group.folded
          ? null
          : group.rows.map((row) => (
              <StoryRow key={row.parent.id} view={view} row={row} onOpen={open} />
            ))}
      </View>
    ));

  if (sections.length === 0) return <View>{groups('all', groupRows(view.rows))}</View>;
  return (
    <View>
      {sections.map((layout) => (
        <View key={layout.section.id} testID={featureBoardSectionId(layout.section.key)}>
          <Text
            accessibilityRole="header"
            style={{
              color: theme.colors.text,
              fontSize: theme.font.size.base,
              fontWeight: theme.font.weight.semibold,
              paddingTop: theme.space[4],
              paddingHorizontal: theme.space[4],
              paddingLeft: theme.space[4] + Math.max(layout.section.depth - 1, 0) * theme.space[4],
            }}
          >
            {sectionTitle(layout)}
          </Text>
          {layout.rows.length === 0 ? (
            <Text
              testID={featureBoardSectionEmptyId(layout.section.key)}
              style={{
                color: theme.colors.muted,
                fontSize: theme.font.size.sm,
                padding: theme.space[4],
              }}
            >
              No stories or bugs yet.
            </Text>
          ) : (
            groups(layout.section.key, groupRows(layout.rows))
          )}
        </View>
      ))}
    </View>
  );
}
