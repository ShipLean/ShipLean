import { getInbox, inboxKeys } from '@shiplean/core/api/inbox';
import { useQuery } from '@tanstack/react-query';
import { type FunctionComponent } from 'react';
import { ScrollView, Text, View } from 'react-native';
import { useTheme } from '../theme';
import { BottomSheet } from '../ui/BottomSheet';
import { Button } from '../ui/Button';
import { firstResultView } from './guide';
import { firstResultRowId, firstResultSheetIds } from './FirstResultSheet.testIds';

export interface FirstResultSheetProps {
  open: boolean;
  projectId: string;
  projectKey: string;
  // Open Inbox: closes the sheet and lands on the Inbox with the tip.
  onOpenInbox: () => void;
  onClose: () => void;
}

// Setup P12: the answer to the first "What needs me?", from the Inbox as it is now. Read-only: nothing was changed.
// On screen only; nothing is read aloud.
export const FirstResultSheet: FunctionComponent<FirstResultSheetProps> = ({
  open,
  projectId,
  onOpenInbox,
  onClose,
}) => {
  const theme = useTheme();
  const { colors } = theme;
  const inbox = useQuery({
    queryKey: inboxKeys.inbox(projectId),
    queryFn: () => getInbox(projectId),
    enabled: open && projectId !== '',
  });
  const view = inbox.data === undefined ? null : firstResultView(inbox.data);
  const lead =
    view !== null
      ? view.lead
      : inbox.isError
        ? "Couldn't load your Inbox just now."
        : 'Checking your Inbox…';
  return (
    <BottomSheet
      testID={firstResultSheetIds.root}
      title="Your first command"
      open={open}
      onClose={onClose}
    >
      <View style={{ gap: theme.space[3] }}>
        <Text style={{ color: colors.muted, fontSize: theme.font.size.base }}>
          &ldquo;What needs me?&rdquo;
        </Text>
        <Text
          testID={firstResultSheetIds.lead}
          style={{
            color: colors.text,
            fontSize: theme.font.size.body15,
            fontWeight: theme.font.weight.semibold,
          }}
        >
          {lead}
        </Text>
        <ScrollView
          testID={firstResultSheetIds.rows}
          style={{ maxHeight: 240 }}
          contentContainerStyle={{ gap: theme.space[2] }}
        >
          {(view?.rows ?? []).map((row) => (
            <View
              key={row.key}
              testID={firstResultRowId(row.key)}
              style={{ flexDirection: 'row', gap: theme.space[2], alignItems: 'baseline' }}
            >
              <Text style={{ color: colors.muted, fontSize: theme.font.size.sm }}>{row.key}</Text>
              <Text
                numberOfLines={1}
                style={{
                  flex: 1,
                  minWidth: 0,
                  color: colors.text,
                  fontSize: theme.font.size.body15,
                }}
              >
                {row.title}
              </Text>
              <Text style={{ color: colors.muted, fontSize: theme.font.size.sm }}>
                {row.counts}
              </Text>
            </View>
          ))}
        </ScrollView>
        <Text
          testID={firstResultSheetIds.unchanged}
          style={{ color: colors.muted, fontSize: theme.font.size.base }}
        >
          From your Inbox as it is now · nothing was changed
        </Text>
        <Text
          testID={firstResultSheetIds.next}
          style={{ color: colors.text, fontSize: theme.font.size.base }}
        >
          Next, open an item and try &ldquo;add a note: …&rdquo;
        </Text>
        <Button testID={firstResultSheetIds.openInbox} label="Open Inbox" onPress={onOpenInbox} />
        <Button testID={firstResultSheetIds.done} label="Done" variant="ghost" onPress={onClose} />
      </View>
    </BottomSheet>
  );
};
