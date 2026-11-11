import type { WorkItemDto } from '@shiplean/core/api/workItems';
import type { FC } from 'react';
import { Text, View } from 'react-native';
import { useTheme } from '../../theme';
import { Button } from '../../ui/Button';
import { countUnder } from '../../ui/progress';
import { useFeatureBoardQuery } from '../queries';
import {
  progressErrorId,
  progressRetryId,
  storyBlockedTaskId,
  storyBoardNoteId,
  storyCountsId,
  storyTasksId,
} from '../testIds';
import { SectionCard } from './SectionCard';
import { taskCounts, type TaskCounts } from './taskCounts';

export interface StoryTasksProps {
  item: WorkItemDto;
  projectId: string;
}

// A Story's tasks as counts only, never a list: done and total from its own roll-ups, and, with a Feature parent, the split of the open ones
// from the Feature's board, naming only the blocked tasks.
export const StoryTasks: FC<StoryTasksProps> = ({ item, projectId }) => {
  const theme = useTheme();
  const parent = item.parent;
  return (
    <View testID={storyTasksId}>
      <SectionCard title="Tasks">
        {parent !== null && parent.type.id === 'feature' ? (
          <WithBoard item={item} projectId={projectId} featureKey={parent.key} />
        ) : (
          <Counts item={item} split={null} />
        )}
        <Text
          testID={storyBoardNoteId}
          style={{ color: theme.colors.muted, fontSize: theme.font.size.sm }}
        >
          Tasks are moved on the web task board.
        </Text>
      </SectionCard>
    </View>
  );
};

// Fails on its own: a failed board read replaces the split with a message and Retry; the done count stays.
function WithBoard({ item, projectId, featureKey }: StoryTasksProps & { featureKey: string }) {
  const theme = useTheme();
  const board = useFeatureBoardQuery(projectId, featureKey);
  if (board.isError) {
    return (
      <>
        <Counts item={item} split={null} />
        <View testID={progressErrorId} accessibilityRole="alert" style={{ gap: theme.space[2] }}>
          <Text style={{ color: theme.colors.danger, fontSize: theme.font.size.md }}>
            Progress couldn't load.
          </Text>
          <Button
            testID={progressRetryId}
            variant="ghost"
            label="Retry"
            onPress={() => void board.refetch()}
          />
        </View>
      </>
    );
  }
  const row = board.data?.rows.find((r) => r.parent.id === item.id);
  return (
    <Counts
      item={item}
      split={board.data === undefined || row === undefined ? null : taskCounts(row, board.data)}
    />
  );
}

function Counts({ item, split }: { item: WorkItemDto; split: TaskCounts | null }) {
  const theme = useTheme();
  const { colors } = theme;
  const { closed, total } = countUnder(item.rollups, 'task');
  const part = (text: string) => ` · ${text}`;
  return (
    <>
      <Text testID={storyCountsId} style={{ color: colors.muted, fontSize: theme.font.size.md }}>
        <Text style={{ color: colors.text, fontWeight: theme.font.weight.semibold }}>
          {closed} of {total} done
        </Text>
        {split === null ? '' : part(`${split.inProgress} in progress`)}
        {split === null ? '' : part(`${split.todo} to do`)}
        {split !== null && split.blocked.length > 0 ? (
          <Text style={{ color: colors.danger }}>{part(`${split.blocked.length} blocked`)}</Text>
        ) : null}
      </Text>
      {split?.blocked.map((task) => (
        <View
          key={task.key}
          testID={storyBlockedTaskId(task.key)}
          style={{
            padding: theme.space[3],
            borderRadius: theme.radius.md,
            borderWidth: 1,
            borderColor: colors.danger,
          }}
        >
          <Text style={{ color: colors.text, fontSize: theme.font.size.md }}>
            {[task.title, task.owner, 'blocked'].filter((p) => p !== null).join(' · ')}
          </Text>
        </View>
      ))}
    </>
  );
}
