import type { WorkItemDto } from '@shiplean/core/api/workItems';
import type { FC } from 'react';
import { Text, View } from 'react-native';
import { useTheme } from '../../theme';
import { countStoryLevelUnder } from '../../ui/progress';
import { ProgressBar } from '../../ui/ProgressBar';
import { useFeatureBoardQuery } from '../queries';
import { compactProgressId, progressBlockedId, progressCountsId } from '../testIds';
import { blockedOnBoard } from './taskCounts';

export interface CompactProgressProps {
  item: WorkItemDto;
  projectId: string;
}

// The header's progress line, from the item's own roll-ups: "1 of 6 stories done · tasks 7/15 · 1 blocked". An Epic reads no board.
export const CompactProgress: FC<CompactProgressProps> = ({ item, projectId }) => {
  const theme = useTheme();
  return (
    <View
      testID={compactProgressId}
      style={{
        marginHorizontal: theme.space[4],
        marginTop: theme.space[2],
        gap: theme.space[2],
      }}
    >
      <ProgressBar rollups={item.rollups} />
      {item.type.id === 'feature' ? (
        <FeatureCounts item={item} projectId={projectId} />
      ) : (
        <CountsLine item={item} tasks={null} blocked={0} />
      )}
    </View>
  );
};

// The counts come from two reads, and each fails on its own: with no board the whole line is hidden.
function FeatureCounts({ item, projectId }: CompactProgressProps) {
  const board = useFeatureBoardQuery(projectId, item.key);
  if (board.data === undefined) return null;
  const { tasksClosed, tasksTotal } = board.data.header;
  return (
    <CountsLine
      item={item}
      tasks={{ closed: tasksClosed, total: tasksTotal }}
      blocked={blockedOnBoard(board.data).length}
    />
  );
}

function CountsLine({
  item,
  tasks,
  blocked,
}: {
  item: WorkItemDto;
  tasks: { closed: number; total: number } | null;
  blocked: number;
}) {
  const theme = useTheme();
  const { colors } = theme;
  const { closed, total } = countStoryLevelUnder(item.rollups);
  return (
    <Text testID={progressCountsId} style={{ color: colors.muted, fontSize: theme.font.size.sm }}>
      <Text style={{ color: colors.text, fontWeight: theme.font.weight.semibold }}>
        {closed} of {total}
      </Text>{' '}
      stories done
      {tasks === null ? '' : ` · tasks ${tasks.closed}/${tasks.total}`}
      {blocked > 0 ? (
        <Text style={{ color: colors.danger, fontWeight: theme.font.weight.semibold }}>
          {' · '}
          <Text testID={progressBlockedId} style={{ color: colors.danger }}>
            {blocked} blocked
          </Text>
        </Text>
      ) : null}
    </Text>
  );
}
