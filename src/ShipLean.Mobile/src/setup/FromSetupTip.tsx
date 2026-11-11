import { getInbox, inboxKeys } from '@shiplean/core/api/inbox';
import { platform } from '@shiplean/core/platform';
import { useQuery } from '@tanstack/react-query';
import { useLocalSearchParams } from 'expo-router';
import { useEffect, useState, type FunctionComponent } from 'react';
import { Text, View } from 'react-native';
import { useProject } from '../shell/useProject';
import { useTheme } from '../theme';
import { Button } from '../ui/Button';
import { fromSetupTipIds } from './FromSetupTip.testIds';
import { markGuide, readGuide } from './guide';

export interface FromSetupTipProps {
  projectKey: string;
}

function TipCard({ projectKey, onGotIt }: FromSetupTipProps & { onGotIt: () => void }) {
  const theme = useTheme();
  const { project } = useProject(projectKey);
  const inbox = useQuery({
    queryKey: inboxKeys.inbox(project?.id ?? ''),
    queryFn: () => getInbox(project?.id ?? ''),
    enabled: project !== undefined,
  });
  const count = inbox.data?.items.length;
  return (
    <View
      testID={fromSetupTipIds.card}
      style={{
        gap: theme.space[2],
        marginTop: theme.space[3],
        marginHorizontal: theme.space[4],
        padding: theme.space[3],
        borderRadius: theme.radius.lg,
        borderWidth: 1,
        borderColor: theme.colors.border,
        backgroundColor: theme.colors.surface,
      }}
    >
      <Text
        testID={fromSetupTipIds.title}
        style={{
          color: theme.colors.text,
          fontSize: theme.font.size.body15,
          fontWeight: theme.font.weight.bold,
        }}
      >
        {count === undefined
          ? 'The items from your answer'
          : `The ${count === 1 ? '1 item' : `${count} items`} from your answer`}
      </Text>
      <Text style={{ color: theme.colors.text, fontSize: theme.font.size.base }}>
        Tap an answer on a row, or hold the mic and say it.
      </Text>
      <Button testID={fromSetupTipIds.gotIt} label="Got it" variant="ghost" onPress={onGotIt} />
    </View>
  );
}

// The one-time tip on the Inbox after setup's Open Inbox (`?from=setup`): the same items as the first answer, and
// how to answer from a row. Shown once per phone. The root stays mounted and empty otherwise, and nothing is read.
export const FromSetupTip: FunctionComponent<FromSetupTipProps> = ({ projectKey }) => {
  const { from } = useLocalSearchParams<{ from?: string }>();
  const [shown, setShown] = useState(false);
  const [gotIt, setGotIt] = useState(false);

  // Arriving from setup, and never shown on this phone before: show it, and keep it until Got it.
  if (!shown && from === 'setup' && !readGuide(platform().persistentStore()).fromSetupTipShown) {
    setShown(true);
  }
  useEffect(() => {
    if (shown) markGuide(platform().persistentStore(), 'fromSetupTipShown');
  }, [shown]);

  return (
    <View testID={fromSetupTipIds.root}>
      {shown && !gotIt ? <TipCard projectKey={projectKey} onGotIt={() => setGotIt(true)} /> : null}
    </View>
  );
};
