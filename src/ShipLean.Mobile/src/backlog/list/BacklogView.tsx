import type { BacklogLevel } from '@shiplean/core/api/backlog';
import { ApiRequestError } from '@shiplean/core/api/client';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import { useRouter } from 'expo-router';
import { useMemo, useState } from 'react';
import { FlatList, RefreshControl, View } from 'react-native';
import { links } from '../../links';
import { useProject } from '../../shell/useProject';
import { useTheme } from '../../theme';
import { EmptyState } from '../../ui/EmptyState';
import { ErrorState } from '../../ui/ErrorState';
import { Chip } from '../../ui/Chip';
import { Segmented } from '../../ui/Segmented';
import { Skeleton } from '../../ui/Skeleton';
import { TabRootBar } from '../../ui/TabRootBar';
import { backlogVoiceSpec } from '../voiceSpec';
import { useScreenVoice } from '../../voice/VoiceProvider';
import { AddRow } from '../add/AddRow';
import { MoveSheet } from '../move/MoveSheet';
import { useBacklogMembers } from '../queries';
import { backlogIds } from '../testIds';
import { BacklogRow } from './BacklogRow';
import {
  backlogFilter,
  isFiltering,
  needsMeRows,
  openingLevel,
  pickerLevel,
  viewChange,
} from './filters';
import { ParentPicker } from './ParentPicker';
import { useBacklogList } from './useBacklogList';
import { useRankMoves } from './useRankMoves';

export interface BacklogViewProps {
  projectKey: string;
  level?: BacklogLevel;
  // Keys, such as SHIP-12.
  epic?: string;
  feature?: string;
}

const LEVELS: { value: BacklogLevel; label: string }[] = [
  { value: 'epics', label: 'Epics' },
  { value: 'features', label: 'Features' },
  { value: 'stories', label: 'Stories' },
];

const NOUN = { features: 'Epic', stories: 'Feature' } as const;

// The Backlog tab: one level in rank order, narrowed by chips. The link's params only set where it opens.
export function BacklogView(props: BacklogViewProps) {
  const { projectKey } = props;
  const theme = useTheme();
  const router = useRouter();
  const { project } = useProject(projectKey);
  const projectId = project?.id;

  const [level, setLevel] = useState(() => openingLevel(props.level, props.epic, props.feature));
  const [needsMe, setNeedsMe] = useState(false);
  const [open, setOpen] = useState(true);
  const [parentKey, setParentKey] = useState<string | null>(() => {
    const opened = openingLevel(props.level, props.epic, props.feature);
    return (
      (opened === 'features' ? props.epic : opened === 'stories' ? props.feature : undefined) ??
      null
    );
  });
  const [pickerOpen, setPickerOpen] = useState(false);
  const [moving, setMoving] = useState<BacklogRowDto | null>(null);

  const from = pickerLevel(level);
  // Every Epic (or Feature) of the project, open or not, to turn a key into an id and to pick from.
  const parents = useBacklogList(
    projectId,
    from ?? 'epics',
    from !== null && (pickerOpen || parentKey !== null)
      ? { mode: 'list', filter: '', includeCompleted: true }
      : null,
  );
  const parent =
    parentKey === null ? null : (parents.data?.rows.find((r) => r.key === parentKey) ?? null);
  const parentPending = parentKey !== null && parents.data === undefined && !parents.isError;
  const unknownParent = parentKey !== null && parents.data !== undefined && parent === null;

  const filter =
    projectId === undefined || parentPending || unknownParent
      ? null
      : backlogFilter(level, { needsMe, open, parentId: parent?.id ?? null });
  const list = useBacklogList(projectId, level, filter);
  const rows = list.data?.rows;
  const needing = useMemo(() => needsMeRows(rows ?? []), [rows]);
  const visible = needsMe ? needing : (rows ?? []);
  const moves = useRankMoves(
    filter === null ? '' : `${level}|${filter.filter}|${filter.includeCompleted}`,
    rows,
  );
  const members = useBacklogMembers(projectId);

  const filtering = isFiltering(level, parent?.id ?? null);
  const narrowed = needsMe || parent !== null;

  const chooseLevel = (next: BacklogLevel) => {
    if (next === level) return;
    setLevel(next);
    setParentKey(null);
  };

  useScreenVoice(
    backlogVoiceSpec({
      projectId: projectId ?? '',
      level,
      rows: visible,
      order: rows ?? [],
      filters: {
        needsMe,
        open,
        epicKey: level === 'features' ? parentKey : null,
        featureKey: level === 'stories' ? parentKey : null,
      },
      features:
        level === 'stories'
          ? (parents.data?.rows ?? []).map(({ id, key, title }) => ({ id, key, title }))
          : [],
      members: members.data ?? [],
    }),
    {
      onView: (view) => {
        const change = viewChange(view.command, view.args);
        if (change === null) return;
        if (change.kind === 'level') return chooseLevel(change.level);
        if (change.chip === 'needsMe') return setNeedsMe(change.on);
        if (change.chip === 'open') return setOpen(change.on);
        if (change.chip === (level === 'features' ? 'epic' : 'feature'))
          setParentKey(change.on ? change.key : null);
      },
    },
  );

  const error = list.error ?? parents.error;
  const refetch = () => {
    void list.refetch();
    if (parents.isError) void parents.refetch();
  };

  const empty = () => {
    if (error) {
      return (
        <ErrorState
          title="Couldn't load the backlog."
          body={
            error instanceof ApiRequestError && error.status === 400 ? error.message : undefined
          }
          action={{ label: 'Retry', busy: list.isFetching || parents.isFetching, onPress: refetch }}
        />
      );
    }
    if (unknownParent) return <EmptyState title="Nothing matches this filter" />;
    if (list.data === undefined) return <Skeleton rows={6} />;
    return <EmptyState title={narrowed ? 'Nothing matches this filter' : 'Nothing to show'} />;
  };

  const noun = level === 'epics' ? null : NOUN[level];
  return (
    <>
      <TabRootBar
        projectKey={projectKey}
        title="Backlog"
        subtitle={project?.name}
        onTilePress={() => router.dismissTo(links.projects())}
      />
      <View
        style={{
          paddingHorizontal: theme.space[4],
          paddingTop: theme.space[2],
          flexDirection: 'row',
        }}
      >
        <Segmented
          options={LEVELS}
          value={level}
          onChange={chooseLevel}
          testID={backlogIds.segment}
        />
      </View>
      <View
        style={{
          flexDirection: 'row',
          flexWrap: 'wrap',
          gap: theme.space[2],
          padding: theme.space[4],
          paddingTop: theme.space[2],
        }}
      >
        <Chip
          label="Needs me"
          count={needing.length}
          selected={needsMe}
          onPress={() => setNeedsMe(!needsMe)}
          testID={backlogIds.chipNeedsMe}
        />
        <Chip
          label="Open"
          selected={open}
          onPress={() => setOpen(!open)}
          testID={backlogIds.chipOpen}
        />
        {noun === null ? null : (
          <Chip
            label={parentKey ?? `All ${noun.toLowerCase()}s`}
            selected={parentKey !== null}
            onPress={() => setPickerOpen(true)}
            testID={backlogIds.chipParent}
          />
        )}
      </View>
      <FlatList
        testID={backlogIds.list}
        data={visible}
        keyExtractor={(row) => row.id}
        refreshControl={<RefreshControl refreshing={list.isRefetching} onRefresh={refetch} />}
        ListHeaderComponent={
          projectId !== undefined && level !== 'epics' ? (
            <AddRow
              projectId={projectId}
              level={level}
              parent={parent === null ? null : { id: parent.id, key: parent.key }}
            />
          ) : undefined
        }
        ListEmptyComponent={empty()}
        renderItem={({ item }) => (
          <BacklogRow
            row={item}
            level={level}
            highlight={moves.get(item.key) ?? null}
            showAsks={needsMe}
            onMove={filtering ? undefined : setMoving}
            onOpen={(row) => router.push(links.item(projectKey, row.key))}
          />
        )}
      />
      {noun === null ? null : (
        <ParentPicker
          noun={noun}
          rows={(parents.data?.rows ?? []).filter(
            (r) => r.state?.category !== 'completed' || r.key === parentKey,
          )}
          selectedKey={parentKey}
          open={pickerOpen}
          onClose={() => setPickerOpen(false)}
          onPick={(row) => {
            setParentKey(row?.key ?? null);
            setPickerOpen(false);
          }}
        />
      )}
      {moving === null ? null : (
        <MoveSheet
          row={moving}
          rows={rows ?? []}
          level={level}
          open
          onClose={() => setMoving(null)}
        />
      )}
    </>
  );
}
