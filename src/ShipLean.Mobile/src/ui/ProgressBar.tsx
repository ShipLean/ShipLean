import type { RollupCountDto } from '@shiplean/core/api/views';
import { View } from 'react-native';
import { useTheme } from '../theme';
import { countStoryLevelUnder } from './progress';
import { primitiveIds } from './testIds';

export interface ProgressBarProps {
  rollups: RollupCountDto[];
}

const percent = (part: number, total: number): `${number}%` => `${(part / total) * 100}%`;

// The backlog's progress line (app.css .progress): story-level work done, then open work in the current sprint,
// then in another sprint; the rest of the track is open work in no sprint.
export function ProgressBar({ rollups }: ProgressBarProps) {
  const theme = useTheme();
  const { colors } = theme;
  const { closed, current, other, total } = countStoryLevelUnder(rollups);
  const segment = (testID: string, count: number, color: string) =>
    count > 0 ? (
      <View
        testID={testID}
        style={{ width: percent(count, total), height: '100%', backgroundColor: color }}
      />
    ) : null;
  return (
    <View
      testID={primitiveIds.progressBar}
      accessibilityRole="progressbar"
      accessibilityValue={{ min: 0, max: total, now: closed }}
      style={{
        flexDirection: 'row',
        height: theme.space[1] + 2,
        borderRadius: theme.radius.xs,
        overflow: 'hidden',
        backgroundColor: colors.surface2,
      }}
    >
      {segment(primitiveIds.progressDone, closed, colors.ok)}
      {segment(primitiveIds.progressCurrent, current, colors.progressCurrentSprint)}
      {segment(primitiveIds.progressOther, other, colors.progressOtherSprint)}
    </View>
  );
}
