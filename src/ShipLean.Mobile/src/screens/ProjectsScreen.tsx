import { getMe, meKey } from '@shiplean/core/api/me';
import { listProjects, projectsKey } from '@shiplean/core/api/projects';
import { useQuery } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { ActivityIndicator, FlatList, Pressable, RefreshControl, Text, View } from 'react-native';
import { links } from '../links';
import { EmptyState } from '../ui/EmptyState';
import { ErrorState } from '../ui/ErrorState';
import { Row } from '../ui/Row';
import { Screen } from '../ui/Screen';
import { TopBar } from '../ui/TopBar';
import { projectRowId, projectsIds } from './projects/testIds';
import { screenIds } from './testIds';

export function ProjectsScreen() {
  const router = useRouter();
  const projects = useQuery({ queryKey: projectsKey, queryFn: listProjects });
  const me = useQuery({ queryKey: meKey, queryFn: getMe });

  const avatar = (
    <Pressable
      testID={projectsIds.avatar}
      accessibilityRole="button"
      accessibilityLabel="Account"
      onPress={() => router.push(links.account())}
    >
      <Text>{me.data?.initials ?? ''}</Text>
    </Pressable>
  );

  return (
    <Screen testID={screenIds.projects}>
      <TopBar title="Projects" right={avatar} />
      <FlatList
        testID={projectsIds.list}
        data={projects.data ?? []}
        keyExtractor={(project) => project.id}
        refreshControl={
          <RefreshControl
            refreshing={projects.isRefetching}
            onRefresh={() => void projects.refetch()}
          />
        }
        ListEmptyComponent={
          projects.isPending ? (
            <View testID={projectsIds.loading}>
              <ActivityIndicator />
            </View>
          ) : projects.isError ? (
            <ErrorState
              title="Couldn't load your projects."
              body={projects.error.message}
              action={{
                label: 'Retry',
                busy: projects.isFetching,
                onPress: () => void projects.refetch(),
              }}
            />
          ) : (
            <EmptyState title="You're not in any projects yet." />
          )
        }
        renderItem={({ item }) => (
          <Row
            title={item.name}
            subtitle={item.key}
            testID={projectRowId(item.key)}
            onPress={() => router.push(links.inbox(item.key))}
          />
        )}
      />
    </Screen>
  );
}
