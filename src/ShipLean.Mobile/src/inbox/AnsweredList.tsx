import type { AnsweredNextDto, AnsweredRowDto } from '@shiplean/core/api/inbox';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { Pressable, Text, View } from 'react-native';
import { links } from '../links';
import { useTheme } from '../theme';
import { Button } from '../ui/Button';
import { EmptyState } from '../ui/EmptyState';
import { ErrorState } from '../ui/ErrorState';
import { Skeleton } from '../ui/Skeleton';
import type { PendingAnswer } from './actions';
import { useInboxActions } from './InboxActions';
import { useAnsweredQuery } from './queries';
import {
  answeredNextId,
  answeredPendingId,
  answeredPendingStatusId,
  answeredRetryId,
  answeredRowId,
  inboxIds,
} from './testIds';

export interface AnsweredListProps {
  projectId: string;
}

const NOTHING_YET = 'No agent has picked it up yet.';

// What happened after the answer, in the server's own labels.
export function nextLine(next: AnsweredNextDto | null): string {
  if (next === null) return NOTHING_YET;
  if (next.kind === 'agentPickedUp')
    return `${next.agentName ?? ''} picked it up · ${next.atLabel}`;
  return `${next.by?.name ?? ''} replied · ${next.atLabel}`;
}

// Answers the runner still holds, newest first. Those in their undo window are on the Waiting row, and a sent one is
// replaced by the server's row.
const SHOWN: ReadonlySet<PendingAnswer['state']> = new Set([
  'queued',
  'sending',
  'uncertain',
  'failed',
]);

export const shownPending = (pending: PendingAnswer[]): PendingAnswer[] =>
  pending.filter((p) => SHOWN.has(p.state)).sort((a, b) => b.createdAt.localeCompare(a.createdAt));

function pendingStatus(p: PendingAnswer): string {
  if (p.state === 'queued') return 'Sends when you’re online';
  if (p.state === 'failed') return p.error === null ? 'Didn’t send' : `Didn’t send · ${p.error}`;
  return 'Sending…';
}

// What you answered in the last 7 days: answers still on the phone first, then the server's rows with what happened
// next. It renders inside the Inbox's ScrollView, which owns pull down.
export function AnsweredList({ projectId }: AnsweredListProps) {
  const router = useRouter();
  const { key: projectKey } = useLocalSearchParams<{ key: string }>();
  const theme = useTheme();
  const { colors } = theme;
  const { pending, retry } = useInboxActions();
  const answered = useAnsweredQuery(projectId);
  const shown = shownPending(pending);
  const rows = answered.data?.rows ?? [];

  const body = () => {
    if (answered.data === undefined)
      return answered.isError ? (
        <ErrorState
          title="Couldn't load your answers."
          body={answered.error.message}
          action={{
            label: 'Retry',
            busy: answered.isFetching,
            onPress: () => void answered.refetch(),
          }}
        />
      ) : (
        <Skeleton rows={5} />
      );
    if (shown.length === 0 && rows.length === 0)
      return <EmptyState title="Nothing answered in the last 7 days." />;
    return null;
  };

  const rowStyle = {
    padding: theme.space[3],
    paddingHorizontal: theme.space[4],
    gap: theme.space[1],
    backgroundColor: colors.surface,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
  } as const;
  const title = (text: string) => (
    <Text
      style={{
        color: colors.text,
        fontSize: theme.font.size.md,
        fontWeight: theme.font.weight.semibold,
      }}
    >
      {text}
    </Text>
  );
  const line = (text: string, testID: string, color: string = colors.muted) => (
    <Text testID={testID} style={{ color, fontSize: theme.font.size.sm }}>
      {text}
    </Text>
  );

  const serverRow = (row: AnsweredRowDto) => {
    const happened = row.next !== null;
    return (
      <Pressable
        key={`${row.itemId}:${row.kind}:${row.answeredAt}`}
        testID={answeredRowId(row.key)}
        accessibilityRole="button"
        onPress={() => router.push(links.item(projectKey, row.key))}
        style={rowStyle}
      >
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: theme.space[2] }}>
          <Text
            style={{
              color: colors.muted,
              fontSize: theme.font.size.sm,
              textTransform: 'capitalize',
            }}
          >
            {row.type}
          </Text>
          <Text style={{ color: colors.muted, fontSize: theme.font.size.sm }}>{row.key}</Text>
          <Text
            numberOfLines={1}
            style={{
              flex: 1,
              color: colors.text,
              fontSize: theme.font.size.sm,
              fontWeight: theme.font.weight.semibold,
            }}
          >
            {row.title}
          </Text>
          <Text style={{ color: colors.muted, fontSize: theme.font.size.sm }}>
            {row.answeredAtLabel}
          </Text>
        </View>
        <Text style={{ color: colors.text, fontSize: theme.font.size.md }}>{row.summary}</Text>
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: theme.space[2] }}>
          <Text
            style={{ color: happened ? colors.ok : colors.muted, fontSize: theme.font.size.sm }}
          >
            {happened ? '✓' : '◷'}
          </Text>
          {line(nextLine(row.next), answeredNextId(row.key), happened ? colors.ok : colors.muted)}
        </View>
      </Pressable>
    );
  };

  const pendingRow = (p: PendingAnswer) => (
    <View key={p.id} testID={answeredPendingId(p.id)} style={rowStyle}>
      {title(`${p.action.key} · ${p.labels.join(', ')}`)}
      {line(
        pendingStatus(p),
        answeredPendingStatusId(p.id),
        p.state === 'failed' ? colors.danger : colors.muted,
      )}
      {p.state === 'failed' && p.retryable ? (
        <Button
          label="Retry"
          variant="ghost"
          testID={answeredRetryId(p.id)}
          onPress={() => retry(p.id)}
        />
      ) : null}
    </View>
  );

  return (
    <View testID={inboxIds.answeredList}>
      {shown.map(pendingRow)}
      {rows.map(serverRow)}
      {body()}
    </View>
  );
}
