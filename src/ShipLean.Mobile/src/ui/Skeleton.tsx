import { View } from 'react-native';
import { useTheme } from '../theme';
import { layout } from './layout';
import { primitiveIds } from './testIds';

export interface SkeletonProps {
  rows: number;
}

// Grey placeholder rows while a list first loads.
export function Skeleton({ rows }: SkeletonProps) {
  const theme = useTheme();
  const { colors } = theme;
  const bar = (width: `${number}%`, height: number) => (
    <View
      style={{ width, height, borderRadius: theme.radius.xs, backgroundColor: colors.surface2 }}
    />
  );
  return (
    <View
      testID={primitiveIds.skeleton}
      accessibilityLabel="Loading"
      accessibilityState={{ busy: true }}
      style={{
        backgroundColor: colors.surface,
        borderTopWidth: 1,
        borderBottomWidth: 1,
        borderColor: colors.border,
      }}
    >
      {Array.from({ length: rows }, (_, i) => (
        <View
          key={i}
          testID={primitiveIds.skeletonRow}
          style={{
            minHeight: layout.listRow,
            justifyContent: 'center',
            gap: theme.space[2],
            paddingHorizontal: theme.space[4],
            borderBottomWidth: i === rows - 1 ? 0 : 1,
            borderBottomColor: colors.border,
          }}
        >
          {bar('70%', theme.font.size.body15)}
          {bar('40%', theme.font.size.base)}
        </View>
      ))}
    </View>
  );
}
