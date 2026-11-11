import type { TypeRef } from '@shiplean/core/api/refs';
import { Text, View } from 'react-native';
import { useTheme } from '../theme';
import { primitiveIds } from './testIds';

export interface TypeChipProps {
  type: TypeRef | 'Feature';
}

const LETTERS = {
  epic: 'E',
  feature: 'F',
  story: 'S',
  bug: 'B',
  task: 'T',
  action: 'A',
  // Business as usual has no letter in the mock-ups; U keeps it apart from Bug.
  bau: 'U',
} as const;

// The small coloured square for a work item's type (app.css .type): 18 square, radius 5, one letter.
export function TypeChip({ type }: TypeChipProps) {
  const { colors, font } = useTheme();
  const id = type === 'Feature' ? 'feature' : type.id;
  const name = type === 'Feature' ? 'Feature' : type.name;
  return (
    <View
      testID={primitiveIds.typeChip}
      accessibilityLabel={name}
      style={{
        width: 18,
        height: 18,
        borderRadius: 5,
        alignItems: 'center',
        justifyContent: 'center',
        backgroundColor: colors.type[id],
      }}
    >
      <Text
        style={{ color: colors.accentFg, fontSize: font.size.xxs, fontWeight: font.weight.bold }}
      >
        {LETTERS[id]}
      </Text>
    </View>
  );
}
