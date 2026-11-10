import type { StateRef } from '@shiplean/core/api/refs';
import { Text, View } from 'react-native';
import { useTheme } from '../theme';
import { layout, tint } from './layout';
import { primitiveIds } from './testIds';

export interface StatePillProps {
  state: StateRef;
}

// A work item's state (app.css .state): a dot and the name in the state category's colour.
export function StatePill({ state }: StatePillProps) {
  const theme = useTheme();
  const colour = theme.stateCategory[state.category];
  return (
    <View
      testID={primitiveIds.statePill}
      style={{
        flexDirection: 'row',
        alignSelf: 'flex-start',
        alignItems: 'center',
        gap: layout.stateGap,
        height: layout.pill,
        paddingHorizontal: theme.space[2],
        borderRadius: layout.pill / 2,
        backgroundColor: tint(colour, 0.14),
      }}
    >
      <View
        style={{
          width: layout.stateDot,
          height: layout.stateDot,
          borderRadius: layout.stateDot / 2,
          backgroundColor: colour,
        }}
      />
      <Text
        numberOfLines={1}
        style={{
          color: colour,
          fontSize: theme.font.size.sm,
          fontWeight: theme.font.weight.semibold,
        }}
      >
        {state.name}
      </Text>
    </View>
  );
}
