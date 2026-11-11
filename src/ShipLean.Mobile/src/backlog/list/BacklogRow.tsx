import type { BacklogRowDto } from '@shiplean/core/api/views';
import { Pressable, Text, View } from 'react-native';
import { useTheme } from '../../theme';
import { Icon } from '../../ui/Icon';
import { HealthPill } from '../../ui/HealthPill';
import { layout, tint } from '../../ui/layout';
import { NeedsPill } from '../../ui/NeedsPill';
import { countStoryLevelUnder, countUnder } from '../../ui/progress';
import { ProgressBar } from '../../ui/ProgressBar';
import { StatePill } from '../../ui/StatePill';
import { TypeChip } from '../../ui/TypeChip';
import type { BacklogLevel } from '../actions';
import {
  backlogAsksId,
  backlogDoneId,
  backlogMoveButtonId,
  backlogMovedId,
  backlogNeedsBarId,
  backlogOwnerId,
  backlogParentId,
  backlogRankId,
  backlogRowId,
  backlogTasksId,
  backlogTypeId,
} from '../testIds';
import { needsLine, type RankMove } from './filters';

export interface BacklogRowProps {
  row: BacklogRowDto;
  level: BacklogLevel;
  // Set on a row whose rank changed since the last read, for 3 s: it is highlighted and says where it came from.
  highlight: RankMove | null;
  // Absent when the list is filtered by an Epic or a Feature: reordering is off then, as on the web.
  onMove?: (row: BacklogRowDto) => void;
  onOpen: (row: BacklogRowDto) => void;
  // Shows the row's asks inline in place of its progress and task lines (the Needs me filter).
  showAsks?: boolean;
}

// app.css .move-btn: 36 square, reaching 44 with hitSlop.
const MOVE_BUTTON = 36;

// One backlog item in rank order (backlog.html .row): rank, title, type and key, state, health, the needs marker, then
// a Feature's or Epic's progress, or a Story's owner, tasks and Feature.
export function BacklogRow({ row, level, highlight, onMove, onOpen, showAsks }: BacklogRowProps) {
  const theme = useTheme();
  const { colors } = theme;
  const needs = row.needsYou;
  const small = { fontSize: theme.font.size.sm, color: colors.muted } as const;
  return (
    <View
      testID={backlogRowId(row.key)}
      style={{
        flexDirection: 'row',
        alignItems: 'flex-start',
        backgroundColor: highlight ? tint(colors.accent, 0.12) : colors.surface,
        borderBottomWidth: 1,
        borderBottomColor: colors.border,
      }}
    >
      {needs ? (
        <View
          testID={backlogNeedsBarId(row.key)}
          // app.css .row.ask::before: inset from the top and bottom, out of the flow so titles line up.
          style={{
            position: 'absolute',
            left: 0,
            top: theme.space[2],
            bottom: theme.space[2],
            width: 3,
            borderTopRightRadius: 2,
            borderBottomRightRadius: 2,
            backgroundColor: colors.warn,
          }}
        />
      ) : null}
      <Pressable
        accessibilityRole="button"
        onPress={() => onOpen(row)}
        style={{
          flex: 1,
          minHeight: layout.listRow,
          flexDirection: 'row',
          gap: theme.space[3],
          paddingVertical: theme.space[2],
          paddingLeft: theme.space[4],
          paddingRight: theme.space[1],
        }}
      >
        <Text
          testID={backlogRankId(row.key)}
          style={{
            width: 22,
            paddingTop: 2,
            textAlign: 'right',
            color: colors.muted,
            fontSize: theme.font.size.sm,
            fontVariant: ['tabular-nums'],
          }}
        >
          {row.rank ?? ''}
        </Text>
        <View style={{ flex: 1, minWidth: 0, gap: theme.space[1] }}>
          <Text
            numberOfLines={2}
            style={{
              color: colors.text,
              fontSize: theme.font.size.body15,
              fontWeight: theme.font.weight.semibold,
            }}
          >
            {row.title}
          </Text>
          <View
            style={{
              flexDirection: 'row',
              flexWrap: 'wrap',
              alignItems: 'center',
              gap: theme.space[2],
            }}
          >
            {row.type ? (
              <View testID={backlogTypeId(row.key)} accessibilityLabel={row.type.name}>
                <TypeChip type={row.type} />
              </View>
            ) : null}
            <Text style={{ ...small, fontFamily: theme.font.mono }}>{row.key}</Text>
            {row.state ? <StatePill state={row.state} /> : null}
            <HealthPill health={row.health} />
            <NeedsPill needs={needs} variant="kinds" />
            {highlight ? (
              <Text
                testID={backlogMovedId(row.key)}
                style={{
                  color: colors.accent,
                  fontSize: theme.font.size.sm,
                  fontWeight: theme.font.weight.semibold,
                }}
              >
                {row.rank !== null && row.rank < highlight.from ? '↑' : '↓'} from {highlight.from}
              </Text>
            ) : null}
          </View>
          {showAsks && needs ? (
            <Text
              testID={backlogAsksId(row.key)}
              style={{ color: colors.warn, fontSize: theme.font.size.base }}
            >
              {needsLine(needs)}
            </Text>
          ) : level === 'stories' ? (
            <StoryLines row={row} />
          ) : (
            <Progress row={row} level={level} />
          )}
        </View>
      </Pressable>
      <View style={{ width: theme.touch.control, alignItems: 'center' }}>
        {onMove ? (
          <Pressable
            testID={backlogMoveButtonId(row.key)}
            accessibilityRole="button"
            accessibilityLabel={`Move ${row.key}`}
            hitSlop={theme.space[1]}
            onPress={() => onMove(row)}
            // app.css .move-btn: 36 square, lifted 4 to sit level with the title.
            style={{
              width: MOVE_BUTTON,
              height: MOVE_BUTTON,
              marginTop: theme.space[2] - theme.space[1],
              marginRight: theme.space[2] - theme.space[1],
              borderRadius: theme.radius.md,
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            <Icon name="rank" size={20} color={highlight ? colors.accent : colors.muted} />
          </Pressable>
        ) : null}
      </View>
    </View>
  );
}

// A Feature's or Epic's progress: the bar, and how many of its Stories (or Features) are done.
function Progress({ row, level }: { row: BacklogRowDto; level: BacklogLevel }) {
  const theme = useTheme();
  const counts =
    level === 'epics' ? countUnder(row.rollups, 'feature') : countStoryLevelUnder(row.rollups);
  if (counts.total === 0) return null;
  return (
    // app.css .prog-line: the bar fills the row, the count sits after it.
    <View style={{ flexDirection: 'row', alignItems: 'center', gap: theme.space[2] }}>
      <View style={{ flex: 1 }}>
        <ProgressBar rollups={row.rollups} />
      </View>
      <Text
        testID={backlogDoneId(row.key)}
        style={{ color: theme.colors.muted, fontSize: theme.font.size.xs }}
      >
        {counts.closed} of {counts.total}
        {level === 'epics' ? ' features done' : ''}
      </Text>
    </View>
  );
}

// A Story's owner, task counts and Feature.
function StoryLines({ row }: { row: BacklogRowDto }) {
  const theme = useTheme();
  const small = { fontSize: theme.font.size.sm, color: theme.colors.muted } as const;
  const tasks = countUnder(row.rollups, 'task');
  return (
    <View
      style={{ flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', gap: theme.space[2] }}
    >
      {row.assignee ? (
        <Text testID={backlogOwnerId(row.key)} style={small}>
          {row.assignee.name}
        </Text>
      ) : null}
      {tasks.total > 0 ? (
        <Text testID={backlogTasksId(row.key)} style={small}>
          Tasks {tasks.closed}/{tasks.total}
        </Text>
      ) : null}
      {row.parent ? (
        <Text testID={backlogParentId(row.key)} numberOfLines={1} style={small}>
          {row.parent.key} {row.parent.title}
        </Text>
      ) : null}
    </View>
  );
}
