import type { FeatureRowDto } from '@shiplean/core/api/featuresOverview';
import type { ParentRefDto } from '@shiplean/core/api/workItems';
import { useRouter } from 'expo-router';
import { useMemo, useState } from 'react';
import { Pressable, RefreshControl, ScrollView, Text, View } from 'react-native';
import { useInboxQuery } from '../../inbox/queries';
import { links } from '../../links';
import { useProject } from '../../shell/useProject';
import { useTheme } from '../../theme';
import { Chip } from '../../ui/Chip';
import { Icon } from '../../ui/Icon';
import { EmptyState } from '../../ui/EmptyState';
import { ErrorState } from '../../ui/ErrorState';
import { Skeleton } from '../../ui/Skeleton';
import { StatePill } from '../../ui/StatePill';
import { TabRootBar } from '../../ui/TabRootBar';
import { VoiceSendFailedBanner } from '../../voice/after/VoiceSendFailedBanner';
import { useScreenVoice } from '../../voice/VoiceProvider';
import { useFeatureStatesQuery, useFeaturesOverviewQuery } from '../queries';
import { featureGroupCountId, featureGroupId, featureGroupNeedsId, featuresIds } from '../testIds';
import { featuresVoiceSpec } from '../voiceSpec';
import { EpicPicker } from './EpicPicker';
import { FeatureRow } from './FeatureRow';
import {
  chipCounts,
  chipFromView,
  epicsOf,
  groupInboxByFeature,
  isFiltered,
  needYouCount,
  openFeatureCount,
  shownGroups,
  type FeatureChip,
} from './filters';
import { NeedsYouView } from './NeedsYouView';
import { useFeatureMembers } from './useFeatureMembers';
import { useFeatureMoves } from './useFeatureMoves';

// The list's bottom padding (app.css .scroll).
const LIST_BOTTOM = 24;

export interface FeaturesViewProps {
  projectKey: string;
}

// The Features tab: resolves the project, then mounts the list with a real project id so no read is sent before it.
export function FeaturesView({ projectKey }: FeaturesViewProps) {
  const router = useRouter();
  const { project, isLoading, error } = useProject(projectKey);
  if (project !== undefined) return <FeaturesList projectId={project.id} projectKey={projectKey} />;
  return (
    <>
      <TabRootBar
        projectKey={projectKey}
        title="Features"
        onTilePress={() => router.dismissTo(links.projects())}
      />
      {isLoading && error === null ? (
        <Skeleton rows={6} />
      ) : (
        <ErrorState title="Couldn't load the Features." />
      )}
    </>
  );
}

interface FeaturesListProps {
  projectId: string;
  projectKey: string;
}

function FeaturesList({ projectId, projectKey }: FeaturesListProps) {
  const theme = useTheme();
  const router = useRouter();
  const { project } = useProject(projectKey);
  const overviewQuery = useFeaturesOverviewQuery(projectId);
  const inbox = useInboxQuery(projectId);
  const states = useFeatureStatesQuery(projectId);
  const members = useFeatureMembers(projectId);
  const overview = overviewQuery.data;

  const [chip, setChip] = useState<FeatureChip>('all');
  const [epicId, setEpicId] = useState<string | null>(null);
  const [pickerOpen, setPickerOpen] = useState(false);
  const [expandedKey, setExpandedKey] = useState<string | null>(null);

  const filter = useMemo(() => ({ chip, epicId }), [chip, epicId]);
  const allRows = useMemo(() => overview?.groups.flatMap((g) => g.rows) ?? [], [overview]);
  const groups = useMemo(
    () =>
      overview === undefined
        ? []
        : shownGroups(overview, chip === 'needsYou' ? { chip: 'all', epicId } : filter),
    [overview, chip, epicId, filter],
  );
  const visibleRows = useMemo(() => groups.flatMap((g) => g.rows), [groups]);
  const inboxItems = useMemo(() => inbox.data?.items ?? [], [inbox.data]);
  // The Features whose headers show: with Needs you on, those with an ask waiting.
  const shownFeatures = useMemo(
    () =>
      chip === 'needsYou'
        ? groupInboxByFeature(visibleRows, inboxItems).map((g) => g.feature)
        : visibleRows,
    [chip, visibleRows, inboxItems],
  );
  const counts = overview === undefined ? null : chipCounts(overview);
  const epics = useMemo(() => (overview === undefined ? [] : epicsOf(overview)), [overview]);
  const epic = epics.find((e) => e.id === epicId) ?? null;
  const moves = useFeatureMoves(overview === undefined ? undefined : allRows);

  const toggleChip = (next: FeatureChip) => {
    setChip(chip === next ? 'all' : next);
    setExpandedKey(null);
  };

  useScreenVoice(
    featuresVoiceSpec({
      projectId,
      board: false,
      focus: allRows.find((r) => r.key === expandedKey) ?? null,
      rows: shownFeatures,
      order: allRows,
      states: states.data ?? [],
      asks: inboxItems,
      members: members.data ?? [],
      projectKey,
      filtering: isFiltered(filter),
    }),
    {
      onView: (view) => {
        const next = chipFromView(view.command, view.args);
        if (next !== null) {
          setChip(next);
          setExpandedKey(null);
        }
      },
    },
  );

  const refresh = () => {
    void overviewQuery.refetch();
    if (chip === 'needsYou') void inbox.refetch();
  };
  const refreshing = overviewQuery.isRefetching || (chip === 'needsYou' && inbox.isRefetching);

  const pickEpic = (picked: ParentRefDto | null) => {
    setEpicId(picked?.id ?? null);
    setExpandedKey(null);
    setPickerOpen(false);
  };

  const body = () => {
    if (overviewQuery.isError && overview === undefined) {
      return (
        <ErrorState
          title="Couldn't load the Features."
          action={{
            label: 'Retry',
            busy: overviewQuery.isFetching,
            onPress: () => void overviewQuery.refetch(),
          }}
        />
      );
    }
    if (overview === undefined) return <Skeleton rows={6} />;
    if (allRows.length === 0 && overview.groups.every((g) => g.count === 0)) {
      return <EmptyState title="No Features yet." />;
    }
    if (chip === 'needsYou') {
      if (inbox.isError && inbox.data === undefined) {
        return (
          <ErrorState
            title="Couldn't load what needs you."
            action={{
              label: 'Retry',
              busy: inbox.isFetching,
              onPress: () => void inbox.refetch(),
            }}
          />
        );
      }
      if (inbox.data === undefined) return <Skeleton rows={4} />;
      const featureRows: FeatureRowDto[] = visibleRows;
      return (
        <NeedsYouView
          features={featureRows}
          items={inbox.data.items}
          projectId={projectId}
          projectKey={projectKey}
        />
      );
    }
    if (groups.length === 0) return <EmptyState title="Nothing matches this filter" />;
    return (
      <>
        {groups.map(({ group, rows, count }) => (
          <View key={group.stateCategory} testID={featureGroupId(group.stateCategory)}>
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
                  testID={featureGroupCountId(group.stateCategory)}
                  style={{ color: theme.colors.accent, fontSize: theme.font.size.sm }}
                >
                  {group.label} · {count}
                </Text>
              ) : (
                <>
                  <StatePill
                    state={{
                      id: group.stateCategory,
                      name: group.label,
                      category: group.stateCategory,
                    }}
                  />
                  <Text
                    testID={featureGroupCountId(group.stateCategory)}
                    style={{ color: theme.colors.muted, fontSize: theme.font.size.sm }}
                  >
                    {count}
                  </Text>
                </>
              )}
              <View style={{ flex: 1 }} />
              {needYouCount(rows) === 0 ? null : (
                <Text
                  testID={featureGroupNeedsId(group.stateCategory)}
                  style={{ color: theme.colors.muted, fontSize: theme.font.size.sm }}
                >
                  {needYouCount(rows)} need you
                </Text>
              )}
            </View>
            {rows.map((row) => (
              <FeatureRow
                key={row.key}
                row={row}
                projectId={projectId}
                projectKey={projectKey}
                expanded={expandedKey === row.key}
                onToggle={() => setExpandedKey(expandedKey === row.key ? null : row.key)}
                highlight={moves.get(row.key) ?? null}
              />
            ))}
          </View>
        ))}
      </>
    );
  };

  const needing = needYouCount(allRows);
  const open = () => router.push(links.digest(projectKey));
  const digest =
    overview === undefined ? null : (
      <Pressable
        testID={featuresIds.sinceBanner}
        accessibilityRole={overview.changedSince === 0 ? undefined : 'button'}
        onPress={overview.changedSince === 0 ? undefined : open}
        style={{
          flexDirection: 'row',
          alignItems: 'center',
          gap: theme.space[3],
          marginHorizontal: theme.space[4],
          marginTop: theme.space[2],
          paddingVertical: theme.space[3],
          paddingHorizontal: theme.space[3],
          borderRadius: 12,
          borderWidth: 1,
          borderColor: theme.colors.border,
          backgroundColor: theme.colors.surface,
        }}
      >
        <Icon name="spark" size={16} color={theme.colors.text} />
        <Text style={{ flex: 1, color: theme.colors.text, fontSize: theme.font.size.sm }}>
          <Text style={{ fontWeight: theme.font.weight.bold }}>
            {overview.changedSince === 0
              ? `No changes ${overview.sinceLabel}`
              : `${overview.changedSince} ${overview.changedSince === 1 ? 'change' : 'changes'} ${overview.sinceLabel}`}
          </Text>
          {needing === 0 ? null : (
            <Text style={{ color: theme.colors.muted }}> · {needing} need you</Text>
          )}
        </Text>
        {overview.changedSince === 0 ? null : (
          <Icon name="chevron" size={16} color={theme.colors.muted} />
        )}
      </Pressable>
    );

  return (
    <>
      <TabRootBar
        projectKey={projectKey}
        title="Features"
        subtitle={
          project === undefined
            ? undefined
            : overview === undefined
              ? project.name
              : `${project.name} · ${openFeatureCount(overview)} open ${openFeatureCount(overview) === 1 ? 'Feature' : 'Features'}`
        }
        onTilePress={() => router.dismissTo(links.projects())}
      />
      <VoiceSendFailedBanner />
      {digest}
      <ScrollView
        horizontal
        showsHorizontalScrollIndicator={false}
        style={{ flexGrow: 0 }}
        contentContainerStyle={{
          gap: theme.space[2],
          padding: theme.space[4],
          paddingBottom: theme.space[2],
        }}
      >
        <Chip
          label="All"
          count={counts?.all}
          selected={chip === 'all'}
          onPress={() => {
            setChip('all');
            setExpandedKey(null);
          }}
          testID={featuresIds.chipAll}
        />
        <Chip
          label="Needs you"
          count={counts?.needsYou}
          selected={chip === 'needsYou'}
          tone={chip === 'needsYou' ? undefined : 'ask'}
          onPress={() => toggleChip('needsYou')}
          testID={featuresIds.chipNeeds}
        />
        <Chip
          label="At risk"
          count={counts?.atRisk}
          selected={chip === 'atRisk'}
          dot={theme.colors.warn}
          onPress={() => toggleChip('atRisk')}
          testID={featuresIds.chipRisk}
        />
        <Chip
          label="Blocked"
          count={counts?.blocked}
          selected={chip === 'blocked'}
          dot={theme.colors.danger}
          onPress={() => toggleChip('blocked')}
          testID={featuresIds.chipBlocked}
        />
        {epics.length === 0 ? null : (
          <Chip
            label={epic?.key ?? 'Epic'}
            selected={epic !== null}
            onPress={() => setPickerOpen(true)}
            testID={featuresIds.chipEpic}
          />
        )}
      </ScrollView>
      <ScrollView
        testID={featuresIds.list}
        contentContainerStyle={{ paddingBottom: LIST_BOTTOM }}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={refresh} />}
      >
        {body()}
      </ScrollView>
      <EpicPicker
        epics={epics}
        selectedId={epicId}
        open={pickerOpen}
        onClose={() => setPickerOpen(false)}
        onPick={pickEpic}
      />
    </>
  );
}
