import type { RetroPointDto } from '@shiplean/core/api/retros';
import { useState } from 'react';
import { Text, View } from 'react-native';
import { useTheme } from '../../theme';
import { BottomSheet } from '../../ui/BottomSheet';
import { Button } from '../../ui/Button';
import { Chip } from '../../ui/Chip';
import { EmptyState } from '../../ui/EmptyState';
import { ErrorState } from '../../ui/ErrorState';
import { Row } from '../../ui/Row';
import { Segmented } from '../../ui/Segmented';
import { Skeleton } from '../../ui/Skeleton';
import { TextField } from '../../ui/TextField';
import { useRetroActionDialogQuery, useRetroMembersQuery } from '../queries';
import { useRetroActions } from '../RetroActions';
import { retroDecideExistingId, retroDecideOwnerId, retroIds } from '../testIds';
import { seedTitle } from './actionTitle';

export interface DecideSheetProps {
  point: RetroPointDto;
  projectId: string;
  // The sprint retro the point is in.
  sprintId: string;
  open: boolean;
  onClose: () => void;
}

type Mode = 'action' | 'existing' | 'notNeeded';

const MODES = [
  { value: 'action', label: 'Action item' },
  { value: 'existing', label: 'Add to existing' },
  { value: 'notNeeded', label: 'Not needed' },
] as const;

// Decide a point: a new action item (the point's text as the title, an owner or none), added to an open action item,
// or not needed (a reason is optional, P-R4). Each is one tap action; the toast carries Undo from there, so the
// sheet closes once the action is dispatched. The form is mounted only while the sheet is open, so each opening
// starts clean.
export function DecideSheet(props: DecideSheetProps) {
  return (
    <BottomSheet
      title="Decide this point"
      open={props.open}
      onClose={props.onClose}
      testID={retroIds.decideSheet}
    >
      {props.open ? <DecideForm {...props} /> : null}
    </BottomSheet>
  );
}

function DecideForm({ point, projectId, sprintId, onClose }: DecideSheetProps) {
  const theme = useTheme();
  const { dispatch } = useRetroActions();
  const [mode, setMode] = useState<Mode>('action');
  const done = (a: Parameters<typeof dispatch>[0]) => {
    dispatch(a);
    onClose();
  };
  return (
    <View style={{ gap: theme.space[3], paddingTop: theme.space[2] }}>
      <Text style={{ color: theme.colors.muted, fontSize: theme.font.size.md }}>{point.text}</Text>
      <View style={{ flexDirection: 'row' }}>
        <Segmented
          options={[...MODES]}
          value={mode}
          onChange={setMode}
          testID={retroIds.decideMode}
        />
      </View>
      {mode === 'action' ? (
        <NewActionForm point={point} projectId={projectId} sprintId={sprintId} onDone={done} />
      ) : mode === 'existing' ? (
        <ExistingActionForm point={point} projectId={projectId} onDone={done} />
      ) : (
        <NotNeededForm point={point} projectId={projectId} sprintId={sprintId} onDone={done} />
      )}
    </View>
  );
}

interface FormProps {
  point: RetroPointDto;
  onDone: (a: Parameters<ReturnType<typeof useRetroActions>['dispatch']>[0]) => void;
}

function NewActionForm({
  point,
  projectId,
  sprintId,
  onDone,
}: FormProps & { projectId: string; sprintId: string }) {
  const theme = useTheme();
  const members = useRetroMembersQuery(projectId);
  const [title, setTitle] = useState(() => seedTitle(point.text));
  const [ownerId, setOwnerId] = useState<string | null>(null);
  const owner = members.data?.find((m) => m.user.id === ownerId)?.user ?? null;
  const text = title.trim();
  return (
    <View style={{ gap: theme.space[3] }}>
      <TextField
        testID={retroIds.decideTitle}
        label="Title"
        value={title}
        onChangeText={setTitle}
      />
      <Text
        style={{
          color: theme.colors.text,
          fontSize: theme.font.size.sm,
          fontWeight: theme.font.weight.semibold,
        }}
      >
        Owner
      </Text>
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: theme.space[2] }}>
        <Chip
          testID={retroIds.decideNoOwner}
          label="No owner"
          selected={ownerId === null}
          onPress={() => setOwnerId(null)}
        />
        {(members.data ?? []).map((m) => (
          <Chip
            key={m.user.id}
            testID={retroDecideOwnerId(m.user.id)}
            label={m.user.name}
            selected={ownerId === m.user.id}
            onPress={() => setOwnerId(m.user.id)}
          />
        ))}
      </View>
      {members.error !== null ? (
        <View testID={retroIds.decideMembersError}>
          <ErrorState
            title="Couldn't load the members."
            action={{
              label: 'Retry',
              busy: members.isFetching,
              onPress: () => void members.refetch(),
            }}
          />
        </View>
      ) : null}
      <Text style={{ color: theme.colors.muted, fontSize: theme.font.size.sm }}>
        Starts in New, no sprint.
      </Text>
      <Button
        testID={retroIds.decideSubmit}
        label="Make action item"
        disabled={text === ''}
        onPress={() =>
          onDone({
            area: 'retro',
            kind: 'newAction',
            itemId: null,
            key: null,
            projectId,
            sprintId,
            pointIds: [point.id],
            title: text,
            ownerId: owner?.id ?? null,
            ownerName: owner?.name ?? null,
            themeId: point.theme?.id ?? null,
          })
        }
      />
    </View>
  );
}

function ExistingActionForm({ point, projectId, onDone }: FormProps & { projectId: string }) {
  const theme = useTheme();
  const dialog = useRetroActionDialogQuery(projectId, { pointIds: [point.id] });
  const [picked, setPicked] = useState<string | null>(null);
  if (dialog.error !== null) {
    return (
      <ErrorState
        title="Couldn't load the open action items."
        action={{ label: 'Retry', busy: dialog.isFetching, onPress: () => void dialog.refetch() }}
      />
    );
  }
  if (dialog.data === undefined) return <Skeleton rows={3} />;
  const open = dialog.data.openActions;
  if (open.length === 0) {
    return <EmptyState title="No open action items." body="Make a new one instead." />;
  }
  // As the web: the newest open action item is chosen until another is tapped.
  const chosen = open.find((a) => a.workItemId === picked) ?? open[0];
  return (
    <View style={{ gap: theme.space[3] }}>
      <View testID={retroIds.decideExisting}>
        {open.map((a) => (
          <Row
            key={a.workItemId}
            testID={retroDecideExistingId(a.key)}
            title={`${a.key} ${a.title}`}
            subtitle={a.key === chosen.key ? 'Chosen' : undefined}
            onPress={() => setPicked(a.workItemId)}
          />
        ))}
      </View>
      <Button
        testID={retroIds.decideSubmit}
        label={`Add to ${chosen.key}`}
        onPress={() =>
          onDone({
            area: 'retro',
            kind: 'addToAction',
            itemId: chosen.workItemId,
            key: chosen.key,
            projectId,
            pointIds: [point.id],
          })
        }
      />
    </View>
  );
}

function NotNeededForm({
  point,
  projectId,
  sprintId,
  onDone,
}: FormProps & { projectId: string; sprintId: string }) {
  const theme = useTheme();
  const [reason, setReason] = useState('');
  return (
    <View style={{ gap: theme.space[3] }}>
      <TextField
        testID={retroIds.decideReason}
        label="Reason (optional)"
        value={reason}
        onChangeText={setReason}
      />
      <Button
        testID={retroIds.decideSubmit}
        label="Not needed"
        onPress={() =>
          onDone({
            area: 'retro',
            kind: 'notNeeded',
            itemId: null,
            key: null,
            projectId,
            sprintId,
            pointId: point.id,
            reason: reason.trim(),
          })
        }
      />
    </View>
  );
}
