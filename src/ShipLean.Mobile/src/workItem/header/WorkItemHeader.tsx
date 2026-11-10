import type { WorkItemDto } from '@shiplean/core/api/workItems';
import { useRouter } from 'expo-router';
import type { FC } from 'react';
import { Pressable, Text, View } from 'react-native';
import { links } from '../../links';
import { useTheme } from '../../theme';
import { HealthPill } from '../../ui/HealthPill';
import { StatePill } from '../../ui/StatePill';
import {
  workItemHeaderId,
  workItemOwnerId,
  workItemParentId,
  workItemTitleId,
  workItemTypeId,
} from '../testIds';

export interface WorkItemHeaderProps {
  item: WorkItemDto;
  projectKey: string;
}

// The header from the design (app.css .wi-head): type and key, the parent, the title, then state, health and owner.
export const WorkItemHeader: FC<WorkItemHeaderProps> = ({ item, projectKey }) => {
  const theme = useTheme();
  const router = useRouter();
  const { colors } = theme;
  const parent = item.parent;
  return (
    <View
      testID={workItemHeaderId}
      style={{
        gap: theme.space[2],
        paddingHorizontal: theme.space[4],
        paddingTop: theme.space[4],
        paddingBottom: theme.space[3],
        backgroundColor: colors.surface,
      }}
    >
      <Text
        testID={workItemTypeId}
        style={{
          color: colors.muted,
          fontSize: theme.font.size.sm,
          fontWeight: theme.font.weight.semibold,
        }}
      >
        {item.type.name} · {item.key}
      </Text>
      {parent === null ? null : (
        <Pressable
          testID={workItemParentId}
          accessibilityRole="link"
          accessibilityLabel={`Parent ${parent.key} ${parent.title}`}
          onPress={() => router.push(links.item(projectKey, parent.key))}
          style={{ minHeight: theme.touch.control, justifyContent: 'center' }}
        >
          <Text numberOfLines={1} style={{ color: colors.accent, fontSize: theme.font.size.md }}>
            {parent.type.name} {parent.key} {parent.title}
          </Text>
        </Pressable>
      )}
      <Text
        testID={workItemTitleId}
        accessibilityRole="header"
        style={{
          color: colors.text,
          fontSize: theme.font.size.lg,
          fontWeight: theme.font.weight.bold,
        }}
      >
        {item.title}
      </Text>
      <View
        style={{
          flexDirection: 'row',
          flexWrap: 'wrap',
          alignItems: 'center',
          gap: theme.space[3],
        }}
      >
        <StatePill state={item.state} />
        <HealthPill health={item.health} />
        <Text
          testID={workItemOwnerId}
          style={{
            color: item.assignee === null ? colors.muted : colors.text,
            fontSize: theme.font.size.sm,
          }}
        >
          {item.assignee === null ? 'No owner' : item.assignee.name}
        </Text>
      </View>
    </View>
  );
};
