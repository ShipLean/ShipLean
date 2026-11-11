import type { DigestItemDto } from '@shiplean/core/api/featuresOverview';
import { useRouter } from 'expo-router';
import { useRef } from 'react';
import {
  RefreshControl,
  ScrollView,
  Text,
  View,
  type NativeScrollEvent,
  type NativeSyntheticEvent,
} from 'react-native';
import { links } from '../../links';
import { useProject } from '../../shell/useProject';
import { useTheme } from '../../theme';
import { Button } from '../../ui/Button';
import { EmptyState } from '../../ui/EmptyState';
import { ErrorState } from '../../ui/ErrorState';
import { Skeleton } from '../../ui/Skeleton';
import { TopBar } from '../../ui/TopBar';
import { featureDigestIds } from '../testIds';
import { DigestItem } from './DigestItem';
import { mergeItems, useDigestPages } from './useDigestPages';
import { useMarkSeen } from './useMarkSeen';

// How far from the end of the list the next page starts to load.
const NEAR_END = 240;

export interface DigestViewProps {
  projectKey: string;
}

// Resolves the project, then mounts the digest with a real project id so no read is sent before it.
export function DigestView({ projectKey }: DigestViewProps) {
  const { project, isLoading, error } = useProject(projectKey);
  if (project !== undefined) return <Digest projectId={project.id} projectKey={projectKey} />;
  return (
    <>
      <Bar projectKey={projectKey} />
      {isLoading && error === null ? (
        <Skeleton rows={6} />
      ) : (
        <ErrorState title="Couldn't load what changed." />
      )}
    </>
  );
}

interface BarProps {
  projectKey: string;
  onSeen?: () => void;
}

function Bar({ projectKey, onSeen }: BarProps) {
  const router = useRouter();
  const theme = useTheme();
  const link = (label: string, testID: string, onPress: () => void) => (
    <Text
      testID={testID}
      accessibilityRole="button"
      onPress={onPress}
      style={{ color: theme.colors.accent, fontSize: theme.font.size.md }}
    >
      {label}
    </Text>
  );
  return (
    <TopBar
      title="What changed"
      left={link('Back', featureDigestIds.back, () => router.dismissTo(links.features(projectKey)))}
      right={
        onSeen === undefined ? undefined : link('Mark as seen', featureDigestIds.seenTop, onSeen)
      }
    />
  );
}

interface DigestProps {
  projectId: string;
  projectKey: string;
}

function Digest({ projectId, projectKey }: DigestProps) {
  const router = useRouter();
  const theme = useTheme();
  const digest = useDigestPages(projectId);
  const markSeen = useMarkSeen(projectId, projectKey);
  const viewport = useRef(0);

  const [firstPage] = digest.pages;
  if (firstPage === undefined) {
    return (
      <>
        <Bar projectKey={projectKey} />
        {digest.first.isError ? (
          <ErrorState
            title="Couldn't load what changed."
            action={{
              label: 'Retry',
              busy: digest.first.isFetching,
              onPress: digest.first.refetch,
            }}
          />
        ) : (
          <Skeleton rows={6} />
        )}
      </>
    );
  }

  const needsYou = mergeItems(digest.pages.map((p) => p.needsYou));
  const changed = mergeItems(digest.pages.map((p) => p.changed));
  const open = (key: string) => router.push(links.item(projectKey, key));
  const section = (testID: string, title: string, items: DigestItemDto[]) =>
    items.length === 0 ? null : (
      <View>
        <Text
          testID={testID}
          accessibilityRole="header"
          style={{
            color: theme.colors.text,
            fontSize: theme.font.size.base,
            fontWeight: theme.font.weight.semibold,
            paddingTop: theme.space[4],
            paddingBottom: theme.space[2],
            paddingHorizontal: theme.space[4],
          }}
        >
          {title}
        </Text>
        {items.map((item) => (
          <DigestItem key={item.id} item={item} onOpen={open} />
        ))}
      </View>
    );

  const onScroll = (e: NativeSyntheticEvent<NativeScrollEvent>) => {
    const { contentOffset, contentSize, layoutMeasurement } = e.nativeEvent;
    if (contentOffset.y + layoutMeasurement.height >= contentSize.height - NEAR_END) {
      digest.loadMore();
    }
  };
  const small = { color: theme.colors.muted, fontSize: theme.font.size.sm } as const;

  return (
    <>
      <Bar projectKey={projectKey} onSeen={markSeen} />
      <ScrollView
        testID={featureDigestIds.scroll}
        refreshControl={
          <RefreshControl refreshing={digest.refreshing} onRefresh={digest.refresh} />
        }
        scrollEventThrottle={64}
        onScroll={onScroll}
        onLayout={(e) => {
          viewport.current = e.nativeEvent.layout.height;
        }}
        // A first page that doesn't fill the screen can't be scrolled, so the next one loads at once.
        onContentSizeChange={(_w, h) => {
          if (viewport.current > 0 && h <= viewport.current) digest.loadMore();
        }}
      >
        <Text
          testID={featureDigestIds.since}
          style={{ ...small, padding: theme.space[4], paddingBottom: 0 }}
        >
          {firstPage.sinceLabel}
        </Text>
        {needsYou.length === 0 && changed.length === 0 ? (
          <View testID={featureDigestIds.empty}>
            <EmptyState title={`Nothing changed ${firstPage.sinceLabel}.`} />
          </View>
        ) : (
          <>
            {section(featureDigestIds.needsYou, 'Needs you', needsYou)}
            {section(featureDigestIds.changed, 'Changed', changed)}
          </>
        )}
        {digest.tail === 'loading' ? (
          <Text
            testID={featureDigestIds.loadingMore}
            style={{ ...small, textAlign: 'center', padding: theme.space[4] }}
          >
            Loading more…
          </Text>
        ) : null}
        {digest.tail === 'error' ? (
          <View style={{ padding: theme.space[4], gap: theme.space[2] }}>
            <Text style={{ ...small, textAlign: 'center' }}>Couldn't load more.</Text>
            <Button
              label="Retry"
              variant="ghost"
              onPress={digest.retryTail}
              testID={featureDigestIds.pageRetry}
            />
          </View>
        ) : null}
        <View style={{ padding: theme.space[4] }}>
          <Button label="Mark as seen" onPress={markSeen} testID={featureDigestIds.seenBottom} />
        </View>
      </ScrollView>
    </>
  );
}
