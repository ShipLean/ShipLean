import type { FeatureRowDto } from '@shiplean/core/api/featuresOverview';
import type { RevisionDto } from '@shiplean/core/api/workItems';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { Text, View } from 'react-native';
import { useInboxActions } from '../../inbox/InboxActions';
import { useInboxSheets } from '../../inbox/InboxSheets';
import { newStatusAsk, statusAsk, toInboxItem } from '../../inbox/toInboxItem';
import { links } from '../../links';
import { useTheme } from '../../theme';
import { Button } from '../../ui/Button';
import {
  useCommentsQuery,
  useHistoryQuery,
  useItemAsksQuery,
  useWorkItemQuery,
} from '../../workItem/queries';
import {
  featureApproveId,
  featureChangeStateId,
  featureDecisionId,
  featureDetailsId,
  featureDiscussId,
  featureHealthButtonId,
  featureHistoryErrorId,
  featureHistoryId,
  featureHistoryLineId,
  featureNewStatusId,
  featureNoteId,
  featureOpenBoardId,
} from '../testIds';
import { ChangeStateSheet } from './ChangeStateSheet';
import { HealthSheet } from './HealthSheet';
import { useRefreshOnSent } from './useRefreshOnSent';

export interface FeatureDetailsProps {
  row: FeatureRowDto;
  projectId: string;
  projectKey: string;
}

const HISTORY_LINES = 3;
const NOTE_LINES = 3;

interface StatusLine {
  text: string;
  by: string;
  at: string;
}

// The last status lines, newest first: each revision that changed the statusLine field, with who and the server's label.
function statusLines(revisions: RevisionDto[]): StatusLine[] {
  return revisions
    .flatMap((r) =>
      r.changes
        .filter((c) => c.field === 'statusLine' && typeof c.to === 'string' && c.to !== '')
        .map((c) => ({
          text: c.to as string,
          by: r.agentName ?? r.changedBy?.name ?? r.webhook ?? '',
          at: r.changedAtLabel,
        })),
    )
    .slice(0, HISTORY_LINES);
}

// An expanded Feature (feature-board.html .more): the latest note, the open decision, the status history, then the
// buttons. Every change is a dispatch: its own actions, or the Inbox's for Approve, and the Inbox's sheets for Discuss
// and New status.
export function FeatureDetails({ row, projectId, projectKey }: FeatureDetailsProps) {
  const theme = useTheme();
  const { colors } = theme;
  const router = useRouter();
  const inbox = useInboxActions();
  const sheets = useInboxSheets();
  const comments = useCommentsQuery(row.id);
  const history = useHistoryQuery(row.id);
  const asks = useItemAsksQuery(projectId, row.key, row.id);
  const item = useWorkItemQuery(projectId, row.key);
  const [stateOpen, setStateOpen] = useState(false);
  const [healthOpen, setHealthOpen] = useState(false);
  useRefreshOnSent(projectId, row.key, row.id);

  const note = [...(comments.data ?? [])].sort(
    (a, b) => Date.parse(b.createdAt) - Date.parse(a.createdAt),
  )[0];
  const decision = (asks.data?.decisions ?? [])
    .filter((d) => d.state === 'open')
    .sort((a, b) => a.number - b.number)[0];
  const lines = history.data === undefined ? [] : statusLines(history.data.revisions);
  const workItem = item.data;

  const label = {
    color: colors.muted,
    fontSize: theme.font.size.xs,
    fontWeight: theme.font.weight.semibold,
    letterSpacing: 0.5,
    textTransform: 'uppercase',
    marginBottom: theme.space[1],
  } as const;
  const body = { color: colors.text, fontSize: theme.font.size.body15 } as const;
  const small = { color: colors.muted, fontSize: theme.font.size.sm } as const;

  const openSheet = (mode: 'discuss' | 'status') => {
    if (workItem === undefined) return;
    const inboxItem = toInboxItem(workItem, asks.data ?? null);
    const ask =
      mode === 'status'
        ? (statusAsk(workItem) ?? newStatusAsk(workItem))
        : inboxItem.asks.find((a) => a.label === decision?.label);
    if (ask === undefined) return;
    sheets.openAnswer({ mode, item: inboxItem, ask, projectId });
  };

  return (
    <View
      testID={featureDetailsId(row.key)}
      style={{
        marginTop: theme.space[3],
        paddingTop: theme.space[3],
        borderTopWidth: 1,
        borderTopColor: colors.border,
        borderStyle: 'dashed',
        gap: theme.space[3],
      }}
    >
      {note === undefined ? null : (
        <View testID={featureNoteId(row.key)}>
          <Text style={label}>Latest note</Text>
          <Text numberOfLines={NOTE_LINES} style={body}>
            {note.body}
          </Text>
          <Text style={small}>
            {note.agentName ?? note.author?.name ?? ''} · {note.createdLabel}
          </Text>
        </View>
      )}
      {decision === undefined ? null : (
        <View testID={featureDecisionId(row.key)} style={{ gap: theme.space[2] }}>
          <Text style={label}>Needs you</Text>
          <Text style={body}>
            {decision.label}: {decision.question}
          </Text>
          <View style={{ flexDirection: 'row', gap: theme.space[2] }}>
            <View style={{ flex: 1 }}>
              <Button
                label="Approve"
                testID={featureApproveId(row.key)}
                onPress={() =>
                  inbox.dispatch({
                    area: 'inbox',
                    projectId,
                    itemId: row.id,
                    key: row.key,
                    kind: 'approve',
                    numbers: [decision.number],
                  })
                }
              />
            </View>
            <View style={{ flex: 1 }}>
              <Button
                label="Discuss"
                variant="ghost"
                testID={featureDiscussId(row.key)}
                disabled={workItem === undefined}
                onPress={() => openSheet('discuss')}
              />
            </View>
          </View>
        </View>
      )}
      {history.isError ? (
        <Text testID={featureHistoryErrorId(row.key)} style={{ ...small, color: colors.danger }}>
          Couldn't load the status history.
        </Text>
      ) : lines.length === 0 ? null : (
        <View testID={featureHistoryId(row.key)}>
          <Text style={label}>Status history</Text>
          {lines.map((line, i) => (
            <View
              key={`${line.at}-${i}`}
              testID={featureHistoryLineId(row.key, i)}
              style={{ flexDirection: 'row', gap: theme.space[2], paddingVertical: 3 }}
            >
              <Text style={{ ...small, color: colors.text, flex: 1 }}>{line.text}</Text>
              <Text style={small}>{line.by === '' ? line.at : `${line.by} · ${line.at}`}</Text>
            </View>
          ))}
        </View>
      )}
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: theme.space[2] }}>
        <View style={{ flexBasis: '48%', flexGrow: 1 }}>
          <Button
            label="Change state"
            variant="ghost"
            testID={featureChangeStateId(row.key)}
            onPress={() => setStateOpen(true)}
          />
        </View>
        <View style={{ flexBasis: '48%', flexGrow: 1 }}>
          <Button
            label="Health"
            variant="ghost"
            testID={featureHealthButtonId(row.key)}
            onPress={() => setHealthOpen(true)}
          />
        </View>
        <View style={{ flexBasis: '48%', flexGrow: 1 }}>
          <Button
            label="New status"
            variant="ghost"
            testID={featureNewStatusId(row.key)}
            disabled={workItem === undefined}
            onPress={() => openSheet('status')}
          />
        </View>
        <View style={{ flexBasis: '48%', flexGrow: 1 }}>
          <Button
            label="Open board"
            variant="ghost"
            testID={featureOpenBoardId(row.key)}
            onPress={() => router.push(links.featureBoard(projectKey, row.key))}
          />
        </View>
      </View>
      <ChangeStateSheet
        row={row}
        projectId={projectId}
        open={stateOpen}
        onClose={() => setStateOpen(false)}
      />
      <HealthSheet row={row} open={healthOpen} onClose={() => setHealthOpen(false)} />
    </View>
  );
}
