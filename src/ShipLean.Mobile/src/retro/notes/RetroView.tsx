import { ApiRequestError } from '@shiplean/core/api/client';
import { getMe, meKey } from '@shiplean/core/api/me';
import type { RetroCategory, RetroPointDto } from '@shiplean/core/api/retros';
import { projectsKey } from '@shiplean/core/api/projects';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { useEffect, useMemo, useRef, useState } from 'react';
import { RefreshControl, ScrollView, View } from 'react-native';
import { usePending } from '../../actions/ActionRunnerProvider';
import { UNDO } from '../../actions/undo';
import { links } from '../../links';
import { useProject } from '../../shell/useProject';
import { useTheme } from '../../theme';
import { Button } from '../../ui/Button';
import { EmptyState } from '../../ui/EmptyState';
import { ErrorState } from '../../ui/ErrorState';
import { Segmented } from '../../ui/Segmented';
import { Skeleton } from '../../ui/Skeleton';
import { TabRootBar } from '../../ui/TabRootBar';
import { VoiceSendFailedBanner } from '../../voice/after/VoiceSendFailedBanner';
import { useScreenVoice } from '../../voice/VoiceProvider';
import { AddPointSheet } from '../add/AddPointSheet';
import { DecideSheet } from '../decide/DecideSheet';
import { ToDecideView } from '../decide/ToDecideView';
import { RetroFeaturesView } from '../features/RetroFeaturesView';
import { useLastTouchedPoint, useShownRetroSprint } from '../lastTouched';
import {
  useInvalidateRetro,
  useRetroInsightsQuery,
  useRetroMembersQuery,
  useSprintRetroQuery,
  useSprintsQuery,
} from '../queries';
import { useRetroActions } from '../RetroActions';
import { retroIds } from '../testIds';
import { retroVoiceSpec } from '../voiceSpec';
import { CategoryTiles } from './CategoryTiles';
import {
  CATEGORY_LABELS,
  countPoints,
  openingCategory,
  orderedColumns,
  retroSprints,
  shownSprint,
  themeGroups,
  viewFromIntent,
  withMySupport,
} from './notesModel';
import { PointCard } from './PointCard';
import { ThemeGroupHeading } from './ThemeGroup';

type Segment = 'notes' | 'toDecide' | 'features';

export interface RetroViewProps {
  projectKey: string;
}

// The Retro tab: the project, then its sprint's retro.
export function RetroView({ projectKey }: RetroViewProps) {
  const router = useRouter();
  const { project, error } = useProject(projectKey);
  const queryClient = useQueryClient();
  if (project === undefined) {
    return (
      <>
        <TabRootBar
          projectKey={projectKey}
          title="Retro"
          onTilePress={() => router.dismissTo(links.projects())}
        />
        {error === null ? (
          <Skeleton rows={6} />
        ) : (
          <ErrorState
            title="Couldn't load the retro."
            action={{
              label: 'Retry',
              onPress: () => void queryClient.invalidateQueries({ queryKey: projectsKey }),
            }}
          />
        )}
      </>
    );
  }
  return <RetroTab projectKey={projectKey} projectId={project.id} />;
}

interface RetroTabProps {
  projectKey: string;
  projectId: string;
}

// The sprint's points by category and theme (Notes), with the To decide and Features segments beside them.
function RetroTab({ projectKey, projectId }: RetroTabProps) {
  const theme = useTheme();
  const router = useRouter();
  const { dispatch } = useRetroActions();
  const touched = useLastTouchedPoint();
  const invalidate = useInvalidateRetro();

  // P-R2: the current sprint, or the ended one while the current one has no point.
  const sprints = useSprintsQuery(projectId);
  const { current } = retroSprints(sprints.data ?? []);
  const currentRetro = useSprintRetroQuery(current?.id ?? null);
  const sprint = sprints.data
    ? shownSprint(sprints.data, currentRetro.data ? countPoints(currentRetro.data) : null)
    : null;
  const endedRetro = useSprintRetroQuery(
    sprint !== null && sprint.id !== current?.id ? sprint.id : null,
  );
  const active = sprint !== null && sprint.id === current?.id ? currentRetro : endedRetro;
  const retro = active.data;
  useShownRetroSprint(retro?.sprint.id ?? null);

  const me = useQuery({ queryKey: meKey, queryFn: () => getMe() }).data;
  const members = useRetroMembersQuery(projectId);
  const insights = useRetroInsightsQuery(projectId, '6');
  const pending = usePending((p) => p.action.area === 'retro');

  const [segment, setSegment] = useState<Segment>('notes');
  // D2: the category opens once per sprint; a person's pick or a voice command replaces it.
  const [view, setView] = useState<{ sprintId: string; category: RetroCategory } | null>(null);
  if (retro !== undefined && view?.sprintId !== retro.sprint.id) {
    setView({ sprintId: retro.sprint.id, category: openingCategory(retro) });
  }
  const category = view?.category ?? 'wentWell';
  const [deciding, setDeciding] = useState<RetroPointDto | null>(null);
  const [adding, setAdding] = useState(false);

  // A point sent, or settled by a retry: read the retro again.
  const sentIds = useRef<Set<string> | null>(null);
  useEffect(() => {
    const sent = pending.filter((p) => p.state === 'sent').map((p) => p.id);
    if (sentIds.current === null) {
      sentIds.current = new Set(sent);
      return;
    }
    const seen = sentIds.current;
    const fresh = sent.filter((id) => !seen.has(id));
    for (const id of fresh) seen.add(id);
    if (fresh.length > 0) void invalidate(projectId);
  }, [pending, invalidate, projectId]);

  const columns = useMemo(() => (retro === undefined ? [] : orderedColumns(retro)), [retro]);
  const column = columns.find((c) => c.category === category);
  const groups = useMemo(
    () =>
      column === undefined
        ? []
        : themeGroups(column).map((group) => ({
            ...group,
            points: group.points.map((p) => withMySupport(p, me ?? null, pending)),
          })),
    [column, me, pending],
  );
  const picked = groups.flatMap((g) => g.points);
  const counts = Object.fromEntries(
    columns.map((c) => [
      c.category,
      { points: c.groups.reduce((n, g) => n + g.points.length, 0), toDecide: c.toDecide },
    ]),
  ) as Record<RetroCategory, { points: number; toDecide: number }>;

  const choose = (next: RetroCategory) => {
    if (retro !== undefined) setView({ sprintId: retro.sprint.id, category: next });
  };

  useScreenVoice(
    retroVoiceSpec({
      // The shown sprint is known before its retro loads, so a spoken point about it is sendable at once.
      sprint:
        retro !== undefined
          ? { id: retro.sprint.id, name: retro.sprint.name }
          : sprint === null
            ? null
            : { id: sprint.id, name: sprint.name },
      category: segment === 'notes' ? category : null,
      points: picked,
      lastTouchedId: touched.pointId,
      openActions: (retro?.actionItems ?? []).map(({ action }) => ({
        workItemId: action.workItemId,
        key: action.key,
        title: action.title,
      })),
      proposals: insights.data?.mergeProposals ?? [],
      members: members.data ?? [],
      projectId,
      projectKey,
    }),
    {
      onView: (intent) => {
        const target = intent.command === 'show' ? viewFromIntent(intent.args) : null;
        if (target === null) return;
        if (target === 'toDecide' || target === 'features') return setSegment(target);
        setSegment('notes');
        choose(target);
      },
    },
  );

  const plus = (point: RetroPointDto) => {
    touched.touch(point.id);
    dispatch(
      {
        area: 'retro',
        kind: point.supportedByMe ? 'unsupport' : 'support',
        itemId: null,
        key: null,
        pointId: point.id,
      },
      { undo: UNDO.none },
    );
  };

  const error = sprints.error ?? active.error;
  const refetch = () => {
    void sprints.refetch();
    void active.refetch();
  };
  const refreshing = (sprints.isRefetching || active.isRefetching) && !active.isPending;

  const body = () => {
    if (error !== null) {
      return (
        <ErrorState
          title="Couldn't load the retro."
          body={
            error instanceof ApiRequestError && error.status === 400 ? error.message : undefined
          }
          action={{
            label: 'Retry',
            busy: sprints.isFetching || active.isFetching,
            onPress: refetch,
          }}
        />
      );
    }
    if (sprints.data === undefined) return <Skeleton rows={6} />;
    if (sprint === null) return <EmptyState title="No sprint yet." />;
    if (retro === undefined) return <Skeleton rows={6} />;
    if (segment === 'toDecide')
      return <ToDecideView projectId={projectId} sprintId={retro.sprint.id} />;
    if (segment === 'features') return <RetroFeaturesView projectId={projectId} />;
    return (
      <ScrollView
        testID={retroIds.list}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={refetch} />}
      >
        <CategoryTiles counts={counts} selected={category} onSelect={choose} />
        {groups.length === 0 ? (
          <EmptyState title={`No points in ${CATEGORY_LABELS[category]} yet.`} />
        ) : (
          groups.map((group) => (
            <View key={group.theme?.id ?? 'unthemed'}>
              <ThemeGroupHeading group={group} />
              {group.points.map((point) => (
                <PointCard
                  key={point.id}
                  point={point}
                  highlighted={point.id === touched.pointId}
                  onTouch={(p) => touched.touch(p.id)}
                  onPlus={plus}
                  onAddAction={(p) => {
                    touched.touch(p.id);
                    setDeciding(p);
                  }}
                  onNotNeeded={(p) => {
                    touched.touch(p.id);
                    setDeciding(p);
                  }}
                  onOpenItem={(key) => router.push(links.item(projectKey, key))}
                />
              ))}
            </View>
          ))
        )}
      </ScrollView>
    );
  };

  const title = retro === undefined ? (sprint?.name ?? 'Retro') : retro.sprint.name;
  return (
    <>
      <TabRootBar
        projectKey={projectKey}
        title={sprint === null ? 'Retro' : `${title} retro`}
        subtitle={
          sprint === null || retro === undefined
            ? undefined
            : `${sprint.datesLabel} · ${retro.kpis.withRetro} ${retro.kpis.withRetro === 1 ? 'retro' : 'retros'} logged`
        }
        onTilePress={() => router.dismissTo(links.projects())}
      />
      <VoiceSendFailedBanner />
      {retro === undefined ? null : (
        <View
          style={{
            flexDirection: 'row',
            alignItems: 'center',
            gap: theme.space[2],
            paddingHorizontal: theme.space[4],
            paddingTop: theme.space[2],
          }}
        >
          <Segmented
            options={[
              { value: 'notes', label: 'Notes' },
              { value: 'toDecide', label: 'To decide', count: retro.kpis.pointsToDecide },
              { value: 'features', label: 'Features' },
            ]}
            value={segment}
            onChange={setSegment}
            testID={retroIds.segment}
          />
        </View>
      )}
      <View style={{ flex: 1 }}>{body()}</View>
      {retro === undefined ? null : (
        <View style={{ padding: theme.space[3] }}>
          <Button
            label="+ Add point"
            variant="ghost"
            onPress={() => setAdding(true)}
            testID={retroIds.addPoint}
          />
        </View>
      )}
      {retro === undefined ? null : (
        <AddPointSheet
          projectId={projectId}
          sprintId={current?.id ?? retro.sprint.id}
          open={adding}
          onClose={() => setAdding(false)}
          category={segment === 'notes' ? category : null}
        />
      )}
      {deciding === null || retro === undefined ? null : (
        <DecideSheet
          point={deciding}
          projectId={projectId}
          sprintId={retro.sprint.id}
          open
          onClose={() => setDeciding(null)}
        />
      )}
    </>
  );
}
