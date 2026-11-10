import type { WorkItemDto } from '@shiplean/core/api/workItems';
import { useState, type FC } from 'react';
import { Pressable, Text, View } from 'react-native';
import { useTheme } from '../../theme';
import { SectionCard } from '../progress/SectionCard';
import {
  goalDescriptionId,
  goalDetailsId,
  goalLinksId,
  goalTextId,
  goalToggleId,
} from '../testIds';

export interface GoalDetailsProps {
  item: WorkItemDto;
}

const GOAL_LINES = 3;

// The goal cut to three lines with the link count; a tap shows the whole goal and the description as plain text. Nothing is editable here.
export const GoalDetails: FC<GoalDetailsProps> = ({ item }) => {
  const theme = useTheme();
  const { colors } = theme;
  const [open, setOpen] = useState(false);
  const links = item.links.length;
  return (
    <View testID={goalDetailsId}>
      <SectionCard title="Goal and details">
        <Pressable
          testID={goalToggleId}
          accessibilityRole="button"
          accessibilityLabel={open ? 'Show less' : 'Read it all'}
          accessibilityState={{ expanded: open }}
          onPress={() => setOpen(!open)}
          style={{ minHeight: theme.touch.control, gap: theme.space[2] }}
        >
          {item.goal === '' ? null : (
            <Text
              testID={goalTextId}
              numberOfLines={open ? undefined : GOAL_LINES}
              style={{ color: colors.text, fontSize: theme.font.size.body15 }}
            >
              {item.goal}
            </Text>
          )}
          {open && item.description !== '' ? (
            <Text
              testID={goalDescriptionId}
              style={{ color: colors.text, fontSize: theme.font.size.body15 }}
            >
              {item.description}
            </Text>
          ) : null}
          <Text testID={goalLinksId} style={{ color: colors.muted, fontSize: theme.font.size.sm }}>
            {links} {links === 1 ? 'link' : 'links'}
          </Text>
        </Pressable>
      </SectionCard>
    </View>
  );
};
