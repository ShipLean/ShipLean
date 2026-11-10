import type { WorkItemDto } from '@shiplean/core/api/workItems';
import type { FC } from 'react';
import { Text, View } from 'react-native';
import { useTheme } from '../../theme';
import { Button } from '../../ui/Button';
import { countStoryLevelUnder } from '../../ui/progress';
import { ProgressBar } from '../../ui/ProgressBar';
import { useFeatureBoardQuery } from '../queries';
import {
  progressErrorId,
  progressLegendId,
  progressRetryId,
  progressSectionId,
  progressTallyId,
} from '../testIds';
import { blockedOnBoard } from './taskCounts';
import { SectionCard } from './SectionCard';

export interface ProgressSectionProps {
  item: WorkItemDto;
  projectId: string;
}

// The Progress card: the full bar with its legend, then the Stories and Tasks counts. The bar and the stories come from
// the item's roll-ups; the task total and the blocked tasks come from the Feature's board. An Epic reads no board.
export const ProgressSection: FC<ProgressSectionProps> = ({ item, projectId }) => {
  const theme = useTheme();
  const { colors } = theme;
  const { closed, current, other, total } = countStoryLevelUnder(item.rollups);
  const legend = (
    id: 'done' | 'current' | 'other' | 'notPlanned',
    colour: string,
    label: string,
    count: number,
  ) => (
    <View
      key={id}
      testID={progressLegendId(id)}
      style={{ flexDirection: 'row', alignItems: 'center', gap: 5 }}
    >
      <View style={{ width: 8, height: 8, borderRadius: 2, backgroundColor: colour }} />
      <Text style={{ color: colors.muted, fontSize: theme.font.size.xs }}>
        {label} {count}
      </Text>
    </View>
  );
  return (
    <View testID={progressSectionId}>
      <SectionCard title="Progress">
        <ProgressBar rollups={item.rollups} />
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', columnGap: 12, rowGap: 4 }}>
          {legend('done', colors.ok, 'Done', closed)}
          {legend('current', colors.progressCurrentSprint, 'In the current sprint', current)}
          {other > 0
            ? legend('other', colors.progressOtherSprint, 'In another sprint', other)
            : null}
          {legend('notPlanned', colors.surface2, 'Not planned', total - closed - current - other)}
        </View>
        <Tally row="stories" label="Stories" value={`${closed}/${total}`} />
        {item.type.id === 'feature' ? <TaskTally item={item} projectId={projectId} /> : null}
      </SectionCard>
    </View>
  );
};

function Tally({
  row,
  label,
  value,
  blocked,
}: {
  row: 'stories' | 'tasks';
  label: string;
  value: string;
  blocked?: number;
}) {
  const theme = useTheme();
  const { colors } = theme;
  return (
    <View
      testID={progressTallyId(row)}
      accessible
      accessibilityLabel={`${label} ${value}`}
      style={{ flexDirection: 'row', alignItems: 'center', gap: theme.space[3] }}
    >
      <Text style={{ color: colors.muted, fontSize: theme.font.size.sm, width: 56 }}>{label}</Text>
      <Text
        style={{
          color: colors.text,
          fontSize: theme.font.size.md,
          fontWeight: theme.font.weight.semibold,
        }}
      >
        {value}
      </Text>
      {blocked !== undefined && blocked > 0 ? (
        <Text style={{ color: colors.danger, fontSize: theme.font.size.sm }}>
          {blocked} blocked
        </Text>
      ) : null}
    </View>
  );
}

// Fails on its own: a failed board read leaves the bar and the stories and shows a message with Retry in their place.
function TaskTally({ item, projectId }: ProgressSectionProps) {
  const theme = useTheme();
  const board = useFeatureBoardQuery(projectId, item.key);
  if (board.isError) {
    return (
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
    );
  }
  if (board.data === undefined) return null;
  const { tasksClosed, tasksTotal } = board.data.header;
  return (
    <Tally
      row="tasks"
      label="Tasks"
      value={`${tasksClosed}/${tasksTotal}`}
      blocked={blockedOnBoard(board.data).length}
    />
  );
}
