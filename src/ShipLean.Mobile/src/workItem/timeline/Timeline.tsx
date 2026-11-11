import type { WorkItemDto } from '@shiplean/core/api/workItems';
import { useEffect, useMemo, useRef, useState } from 'react';
import type { FC, ReactNode } from 'react';
import { Text, View } from 'react-native';
import { useTheme } from '../../theme';
import { Button } from '../../ui/Button';
import { Segmented } from '../../ui/Segmented';
import { SectionCard } from '../progress/SectionCard';
import { useCommentsQuery, useHistoryQuery } from '../queries';
import {
  timelineAgentTagId,
  timelineBodyId,
  timelineChangeId,
  timelineEditedId,
  timelineEmptyId,
  timelineEntryId,
  timelineErrorId,
  timelineFilterId,
  timelineId,
  timelineRetryId,
  timelineSupersededId,
  timelineWhenId,
  timelineWhoId,
} from '../testIds';
import {
  askLine,
  changeLine,
  filterTimeline,
  isChange,
  mergeTimeline,
  type TimelineEntry,
  type TimelineFilter,
} from './timeline';

export interface TimelineProps {
  item: WorkItemDto;
}

// How long entries that arrived with a refetch stay highlighted.
const HIGHLIGHT_MS = 3000;

// The Notes card: the item's comments and its history (field changes and ask events) as one list, newest first, filtered by
// All / Notes / Changes. Dates are the server's labels. A voice note is its approved text only. Each half fails on its own.
export const Timeline: FC<TimelineProps> = ({ item }) => {
  const theme = useTheme();
  const { colors } = theme;
  const comments = useCommentsQuery(item.id);
  const history = useHistoryQuery(item.id);
  const [filter, setFilter] = useState<TimelineFilter>('all');
  const entries = useMemo(
    () => mergeTimeline(comments.data, history.data),
    [comments.data, history.data],
  );
  const fresh = useFresh(entries, !comments.isPending && !history.isPending);
  const shown = filterTimeline(entries, filter);
  const notes = entries.filter((entry) => !isChange(entry)).length;
  const failed = {
    comments: comments.isError && filter !== 'changes',
    history: history.isError && filter !== 'notes',
  };
  const settled = !comments.isPending && !history.isPending;
  return (
    <View testID={timelineId}>
      <SectionCard title="Notes">
        <View style={{ flexDirection: 'row' }}>
          <Segmented
            testID={timelineFilterId}
            value={filter}
            onChange={setFilter}
            options={[
              { value: 'all', label: 'All', count: entries.length },
              { value: 'notes', label: 'Notes', count: notes },
              { value: 'changes', label: 'Changes', count: entries.length - notes },
            ]}
          />
        </View>
        {failed.comments ? (
          <Failure
            half="comments"
            message="The comments could not be loaded."
            onRetry={() => void comments.refetch()}
          />
        ) : null}
        {failed.history ? (
          <Failure
            half="history"
            message="The history could not be loaded."
            onRetry={() => void history.refetch()}
          />
        ) : null}
        {settled && shown.length === 0 && !failed.comments && !failed.history ? (
          <Text
            testID={timelineEmptyId}
            style={{ color: colors.muted, fontSize: theme.font.size.md }}
          >
            {item.type.id === 'feature' ? 'No log entries yet.' : 'No comments yet.'}
          </Text>
        ) : null}
        {shown.map((entry) => (
          <Entry key={entry.id} entry={entry} fresh={fresh.has(entry.id)} />
        ))}
      </SectionCard>
    </View>
  );
};

// The ids that were not in the previous list, for HIGHLIGHT_MS after a refetch brought them. The first list is never highlighted.
function useFresh(entries: TimelineEntry[], settled: boolean): ReadonlySet<string> {
  const seen = useRef<Set<string> | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const [fresh, setFresh] = useState<ReadonlySet<string>>(new Set());
  useEffect(() => {
    if (!settled) return;
    const previous = seen.current;
    seen.current = new Set(entries.map((entry) => entry.id));
    if (previous === null) return;
    const added = entries.map((entry) => entry.id).filter((id) => !previous.has(id));
    if (added.length === 0) return;
    if (timer.current !== null) clearTimeout(timer.current);
    setFresh(new Set(added));
    timer.current = setTimeout(() => setFresh(new Set()), HIGHLIGHT_MS);
  }, [entries, settled]);
  useEffect(
    () => () => {
      if (timer.current !== null) clearTimeout(timer.current);
    },
    [],
  );
  return fresh;
}

function Failure({
  half,
  message,
  onRetry,
}: {
  half: 'comments' | 'history';
  message: string;
  onRetry: () => void;
}) {
  const theme = useTheme();
  return (
    <View testID={timelineErrorId(half)} accessibilityRole="alert" style={{ gap: theme.space[2] }}>
      <Text style={{ color: theme.colors.danger, fontSize: theme.font.size.md }}>{message}</Text>
      <Button testID={timelineRetryId(half)} variant="ghost" label="Retry" onPress={onRetry} />
    </View>
  );
}

function Entry({ entry, fresh }: { entry: TimelineEntry; fresh: boolean }) {
  const theme = useTheme();
  const { colors } = theme;
  const { id } = entry;
  const small = { color: colors.muted, fontSize: theme.font.size.sm };
  let who: string;
  let agent: string | null;
  let when: string;
  let body: ReactNode;
  if (entry.kind === 'note') {
    const { comment } = entry;
    agent = comment.agentName;
    who = agent ?? comment.author?.name ?? 'Unknown author';
    when = comment.createdLabel;
    body = (
      <Text
        testID={timelineBodyId(id)}
        style={{
          color: comment.superseded ? colors.muted : colors.text,
          fontSize: theme.font.size.md,
          textDecorationLine: comment.superseded ? 'line-through' : 'none',
        }}
      >
        {comment.body}
      </Text>
    );
  } else if (entry.kind === 'revision') {
    const { revision } = entry;
    agent = revision.agentName;
    who = agent ?? revision.changedBy?.name ?? revision.webhook ?? 'Jira import';
    when = revision.changedAtLabel;
    body = revision.changes.map((change, index) => (
      <Text
        key={change.field}
        testID={timelineChangeId(id, index)}
        style={{ color: colors.text, fontSize: theme.font.size.md }}
      >
        {changeLine(change)}
      </Text>
    ));
  } else {
    const { line } = entry;
    agent = line.agentName;
    who = agent ?? line.by?.name ?? 'Unknown author';
    when = line.atLabel;
    body = (
      <Text
        testID={timelineChangeId(id, 0)}
        style={{ color: colors.text, fontSize: theme.font.size.md }}
      >
        {askLine(line)}
      </Text>
    );
  }
  const comment = entry.kind === 'note' ? entry.comment : null;
  return (
    <View
      testID={timelineEntryId(id)}
      style={{
        gap: 3,
        paddingVertical: theme.space[2],
        paddingHorizontal: theme.space[2],
        borderRadius: theme.radius.sm,
        backgroundColor: fresh ? colors.accentSoft : 'transparent',
      }}
    >
      <View style={{ flexDirection: 'row', alignItems: 'center', flexWrap: 'wrap', gap: 6 }}>
        <Text
          testID={timelineWhoId(id)}
          style={{ ...small, color: colors.text, fontWeight: theme.font.weight.semibold }}
        >
          {who}
        </Text>
        {agent === null ? null : (
          <Text
            testID={timelineAgentTagId(id)}
            style={{
              color: colors.info,
              fontSize: theme.font.size.xs,
              fontWeight: theme.font.weight.semibold,
            }}
          >
            Agent
          </Text>
        )}
        <Text testID={timelineWhenId(id)} style={small}>
          {when}
        </Text>
        {comment?.editedLabel ? (
          <Text testID={timelineEditedId(id)} style={small}>
            edited {comment.editedLabel}
          </Text>
        ) : null}
        {comment?.superseded ? (
          <Text testID={timelineSupersededId(id)} style={small}>
            Superseded
          </Text>
        ) : null}
      </View>
      {body}
    </View>
  );
}
