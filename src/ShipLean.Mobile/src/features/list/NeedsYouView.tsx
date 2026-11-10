import type { FeatureRowDto } from '@shiplean/core/api/featuresOverview';
import type { InboxItemDto } from '@shiplean/core/api/inbox';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { Text, View } from 'react-native';
import { links } from '../../links';
import { useInboxActions } from '../../inbox/InboxActions';
import { InboxRow } from '../../inbox/InboxRow';
import { useTheme } from '../../theme';
import { EmptyState } from '../../ui/EmptyState';
import { featureNeedsHeaderId, featuresIds } from '../testIds';
import { groupInboxByFeature } from './filters';

export interface NeedsYouViewProps {
  features: FeatureRowDto[];
  items: InboxItemDto[];
  projectId: string;
  projectKey: string;
}

// The asks waiting on you, each Feature's under its header (Stories' under their Feature), as the Inbox shows them.
export function NeedsYouView({ features, items, projectId, projectKey }: NeedsYouViewProps) {
  const theme = useTheme();
  const router = useRouter();
  const { pending } = useInboxActions();
  const [open, setOpen] = useState<string | null>(null);
  const groups = groupInboxByFeature(features, items);
  if (groups.length === 0) {
    return (
      <View testID={featuresIds.needsEmpty}>
        <EmptyState title="Nothing is waiting on you." />
      </View>
    );
  }
  return (
    <>
      {groups.map(({ feature, items: asked }) => (
        <View key={feature.key}>
          <View
            testID={featureNeedsHeaderId(feature.key)}
            style={{
              flexDirection: 'row',
              gap: theme.space[2],
              paddingTop: theme.space[4],
              paddingBottom: theme.space[2],
              paddingHorizontal: theme.space[4],
            }}
          >
            <Text
              style={{
                color: theme.colors.muted,
                fontSize: theme.font.size.sm,
                fontFamily: theme.font.mono,
              }}
            >
              {feature.key}
            </Text>
            <Text
              numberOfLines={1}
              style={{
                flex: 1,
                color: theme.colors.text,
                fontSize: theme.font.size.sm,
                fontWeight: theme.font.weight.semibold,
              }}
            >
              {feature.title}
            </Text>
          </View>
          {asked.map((item) => (
            <InboxRow
              key={item.key}
              projectId={projectId}
              item={item}
              pending={pending.filter((p) => p.action.itemId === item.id)}
              indented={item.key !== feature.key}
              expanded={open === item.key}
              onToggleExpanded={() => setOpen(open === item.key ? null : item.key)}
              onOpenItem={() => router.push(links.item(projectKey, item.key))}
            />
          ))}
        </View>
      ))}
    </>
  );
}
