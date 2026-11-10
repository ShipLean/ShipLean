import type { WorkItemDto } from '@shiplean/core/api/workItems';
import type { FC } from 'react';
import { Text, View } from 'react-native';
import { useTheme } from '../../theme';
import { SectionCard } from '../progress/SectionCard';
import { acceptanceCriteriaId, criterionId } from '../testIds';

export interface AcceptanceCriteriaProps {
  item: WorkItemDto;
}

// The checklist as the web keeps it, read only: done items are ticked. Nothing shows when there are none.
export const AcceptanceCriteria: FC<AcceptanceCriteriaProps> = ({ item }) => {
  const theme = useTheme();
  const { colors } = theme;
  return (
    <View testID={acceptanceCriteriaId}>
      {item.acceptanceCriteria.length === 0 ? null : (
        <SectionCard title="Acceptance criteria">
          {item.acceptanceCriteria.map((criterion, index) => (
            <View
              // The list has no ids; it is read only, so the index is stable.
              key={index}
              testID={criterionId(index)}
              accessible
              accessibilityRole="text"
              accessibilityLabel={`${criterion.done ? 'Done' : 'Not done'}: ${criterion.text}`}
              style={{ flexDirection: 'row', gap: theme.space[2] }}
            >
              <Text
                style={{
                  color: criterion.done ? colors.ok : colors.muted,
                  fontSize: theme.font.size.md,
                }}
              >
                {criterion.done ? '✓' : '○'}
              </Text>
              <Text style={{ flex: 1, color: colors.text, fontSize: theme.font.size.body15 }}>
                {criterion.text}
              </Text>
            </View>
          ))}
        </SectionCard>
      )}
    </View>
  );
};
