import type { FeatureBoardRowDto, FeatureBoardViewDto } from '@shiplean/core/api/views';
import { Pressable, Text, View } from 'react-native';
import { useTheme } from '../../theme';
import { layout } from '../../ui/layout';
import { NeedsPill } from '../../ui/NeedsPill';
import {
  featureBoardOwnerId,
  featureBoardSprintId,
  featureBoardStoryId,
  featureBoardTaskBarId,
  featureBoardTaskDoingId,
  featureBoardTaskDoneId,
  featureBoardTaskTextId,
  featureBoardTypeId,
} from '../testIds';
import { taskBar } from './boardModel';

export interface StoryRowProps {
  view: FeatureBoardViewDto;
  row: FeatureBoardRowDto;
  onOpen: (key: string) => void;
}

const percent = (part: number, total: number): `${number}%` => `${(part / total) * 100}%`;

// One story or bug of the board (feature-board.html .srow): type, key, sprint, Needs you and owner, the title, and its
// tasks as one bar and a count. There are no task cards. A tap opens the item.
export function StoryRow({ view, row, onOpen }: StoryRowProps) {
  const theme = useTheme();
  const { colors } = theme;
  const story = row.parent;
  const bar = taskBar(view, row);
  const total = bar.done + bar.doing + bar.todo;
  const small = { fontSize: theme.font.size.sm, color: colors.muted } as const;
  return (
    <Pressable
      testID={featureBoardStoryId(story.key)}
      accessibilityRole="button"
      onPress={() => onOpen(story.key)}
      style={{
        minHeight: layout.listRow,
        paddingVertical: theme.space[3],
        paddingHorizontal: theme.space[4],
        gap: theme.space[1],
        backgroundColor: colors.surface,
        borderBottomWidth: 1,
        borderBottomColor: colors.border,
      }}
    >
      <View
        style={{
          flexDirection: 'row',
          alignItems: 'center',
          gap: theme.space[2],
        }}
      >
        <Text testID={featureBoardTypeId(story.key)} style={small}>
          {story.type.name}
        </Text>
        <Text style={{ ...small, fontFamily: theme.font.mono }}>{story.key}</Text>
        <View
          testID={featureBoardSprintId(story.key)}
          style={{
            height: 20,
            paddingHorizontal: 7,
            borderRadius: 10,
            justifyContent: 'center',
            backgroundColor: row.sprint === null ? 'transparent' : colors.accentSoft,
            borderWidth: row.sprint === null ? 1 : 0,
            borderStyle: 'dashed',
            borderColor: colors.border,
          }}
        >
          <Text
            style={{
              fontSize: theme.font.size.xs,
              fontWeight: theme.font.weight.semibold,
              color: row.sprint === null ? colors.muted : colors.text,
            }}
          >
            {row.sprint === null ? 'Backlog' : row.sprint.name}
          </Text>
        </View>
        <View style={{ flex: 1 }} />
        <NeedsPill needs={row.needsYou} />
        {story.assignee === null ? null : (
          <Text testID={featureBoardOwnerId(story.key)} numberOfLines={1} style={small}>
            {story.assignee.name}
          </Text>
        )}
      </View>
      <Text
        numberOfLines={3}
        style={{
          color: colors.text,
          fontSize: theme.font.size.body15,
          fontWeight: theme.font.weight.medium,
        }}
      >
        {story.title}
      </Text>
      {total === 0 ? (
        <Text
          testID={featureBoardTaskTextId(story.key)}
          style={{ ...small, fontStyle: 'italic', marginTop: theme.space[1] }}
        >
          No tasks yet
        </Text>
      ) : (
        <View
          style={{
            flexDirection: 'row',
            alignItems: 'center',
            gap: theme.space[3],
            marginTop: theme.space[1],
          }}
        >
          <View
            testID={featureBoardTaskBarId(story.key)}
            style={{
              flexDirection: 'row',
              width: 96,
              height: layout.progress,
              borderRadius: theme.radius.xs,
              overflow: 'hidden',
              backgroundColor: colors.surface2,
            }}
          >
            {bar.done > 0 ? (
              <View
                testID={featureBoardTaskDoneId(story.key)}
                style={{
                  width: percent(bar.done, total),
                  backgroundColor: theme.stateCategory.completed,
                }}
              />
            ) : null}
            {bar.doing > 0 ? (
              <View
                testID={featureBoardTaskDoingId(story.key)}
                style={{
                  width: percent(bar.doing, total),
                  backgroundColor: theme.stateCategory.inProgress,
                }}
              />
            ) : null}
          </View>
          <Text
            testID={featureBoardTaskTextId(story.key)}
            style={{ ...small, fontSize: theme.font.size.xs, fontVariant: ['tabular-nums'] }}
          >
            {bar.done} done · {bar.doing} doing · {bar.todo} to do
          </Text>
        </View>
      )}
    </Pressable>
  );
}
