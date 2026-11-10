import * as Device from 'expo-device';
import { useRouter } from 'expo-router';
import { useState, type ReactNode } from 'react';
import { Platform, ScrollView, Switch, Text, View } from 'react-native';
import { links } from '../links';
import { useTheme } from '../theme';
import { BottomSheet } from '../ui/BottomSheet';
import { Button } from '../ui/Button';
import { Row } from '../ui/Row';
import { engines } from '../voice/engines';
import { MicPermissionSheet } from '../voice/sheets/MicPermissionSheet';
import { ChangeModelSheet } from './ChangeModelSheet';
import { formatGb, modelChoiceView } from './modelChoice';
import { ProgressLine } from './SetupParts';
import { storageRowId, voiceSettingsIds } from './VoiceSettings.testIds';
import { useMicStatus } from './useMicStatus';
import { useModelStatus } from './useModelStatus';
import {
  changeUnderWay,
  commandModelLine,
  licencesLine,
  micIsOn,
  storageView,
  voiceStatusView,
} from './voiceSettingsView';

const REASON = {
  wifi: 'waiting for Wi-Fi',
  space: 'waiting for space',
  network: 'waiting for the network',
  checksum: 'checking again',
  memory: 'waiting',
} as const;

function Group({ title, children }: { title: string; children: ReactNode }) {
  const theme = useTheme();
  return (
    <View>
      <Text
        style={{
          padding: theme.space[2],
          paddingTop: theme.space[3],
          color: theme.colors.muted,
          fontSize: theme.font.size.sm,
          fontWeight: theme.font.weight.semibold,
          textTransform: 'uppercase',
        }}
      >
        {title}
      </Text>
      {children}
    </View>
  );
}

export function VoiceSettings() {
  const router = useRouter();
  const theme = useTheme();
  const { colors } = theme;
  const store = engines().models;
  const status = useModelStatus();
  const { mic, refresh } = useMicStatus();
  const [changing, setChanging] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [micSheet, setMicSheet] = useState(false);
  const memory = store.deviceMemory();
  const choiceView = modelChoiceView(memory);
  const card = voiceStatusView(status, mic, {
    model: Device.modelName ?? 'This phone',
    memoryGb: memory.soldGb,
  });
  const underWay = changeUnderWay(status);
  const storage = storageView(status);
  const micOn = mic !== null && micIsOn(mic);
  const where = Platform.OS === 'android' ? "the app's settings" : 'iPhone Settings';
  const small = { color: colors.muted, fontSize: theme.font.size.base } as const;

  return (
    <ScrollView
      testID={voiceSettingsIds.root}
      contentContainerStyle={{ padding: theme.space[4], paddingBottom: theme.space[5] }}
    >
      <View
        testID={voiceSettingsIds.status}
        style={{
          gap: theme.space[2],
          padding: theme.space[3],
          borderRadius: theme.radius.md,
          borderWidth: 1,
          borderColor: colors.border,
          backgroundColor: colors.surface,
        }}
      >
        <Text style={{ color: colors.text, fontSize: theme.font.size.body15 }}>{card.line}</Text>
        {card.percent === null ? null : (
          <ProgressLine testID={`${voiceSettingsIds.status}-bar`} fraction={card.percent / 100} />
        )}
        {card.setUp ? (
          <Button
            testID={voiceSettingsIds.setUp}
            label="Set up"
            onPress={() => router.push(links.setup('models'))}
          />
        ) : null}
      </View>

      <Group title="Models">
        <Row
          testID={voiceSettingsIds.commandModel}
          title="Command model"
          subtitle={commandModelLine(status, choiceView)}
          right={<Text style={small}>Change</Text>}
          onPress={() => setChanging(true)}
        />
        {underWay === null ? null : (
          <View
            testID={voiceSettingsIds.change}
            style={{ gap: theme.space[1], padding: theme.space[3] }}
          >
            <Text style={small}>
              {`Downloading ${underWay.name} · ${underWay.percent}%${
                underWay.reason === null ? '' : ` · ${REASON[underWay.reason]}`
              }`}
            </Text>
            <ProgressLine testID={voiceSettingsIds.changeBar} fraction={underWay.percent / 100} />
          </View>
        )}
        <Row
          testID={voiceSettingsIds.speech}
          title="Speech to text"
          subtitle={choiceView.speech.name}
          right={<Text style={small}>Fixed</Text>}
        />
        <View style={{ opacity: 0.55 }}>
          <Row
            testID={voiceSettingsIds.tryOther}
            title="Try other models"
            right={<Text style={small}>Later</Text>}
          />
        </View>
      </Group>

      <Group title="Download and storage">
        <View
          style={{
            minHeight: theme.touch.row,
            flexDirection: 'row',
            alignItems: 'center',
            gap: theme.space[3],
            paddingHorizontal: theme.space[4],
            backgroundColor: colors.surface,
            borderBottomWidth: 1,
            borderBottomColor: colors.border,
          }}
        >
          <Text
            style={{
              flex: 1,
              color: colors.text,
              fontSize: theme.font.size.md,
              fontWeight: theme.font.weight.semibold,
            }}
          >
            Wi-Fi only
          </Text>
          <Switch
            testID={voiceSettingsIds.wifiOnly}
            accessibilityLabel="Wi-Fi only"
            value={status.wifiOnly}
            onValueChange={(on) => store.setWifiOnly(on)}
          />
        </View>
        {storage.rows.map((r) => (
          <Row
            key={r.model}
            testID={storageRowId(r.model)}
            title={r.name}
            right={<Text style={small}>{formatGb(r.bytes)}</Text>}
          />
        ))}
        <Row
          testID={voiceSettingsIds.storageTotal}
          title="Used by voice"
          right={<Text style={small}>{formatGb(storage.totalBytes)}</Text>}
        />
        <View style={{ paddingVertical: theme.space[3] }}>
          <Button
            testID={voiceSettingsIds.deleteModels}
            label="Delete models"
            variant="danger"
            disabled={storage.totalBytes === 0}
            onPress={() => setDeleting(true)}
          />
        </View>
      </Group>

      <Group title="Privacy and licences">
        <Row
          testID={voiceSettingsIds.microphone}
          title="Microphone"
          subtitle={micOn || mic === null ? undefined : `Off in ${where}. Tap to turn it on`}
          right={
            mic === null ? undefined : (
              <Text
                testID={voiceSettingsIds.microphoneValue}
                style={{
                  color: micOn ? colors.muted : colors.warn,
                  fontSize: theme.font.size.base,
                  fontWeight: micOn ? theme.font.weight.regular : theme.font.weight.semibold,
                }}
              >
                {micOn ? 'On' : 'Off'}
              </Text>
            )
          }
          onPress={() => {
            if (mic === null) return;
            if (micOn) engines().mic.openSettings();
            else setMicSheet(true);
          }}
        />
        <Row
          testID={voiceSettingsIds.licences}
          title="Model licences"
          subtitle={licencesLine()}
          onPress={() => router.push(links.licences())}
        />
      </Group>

      {changing ? <ChangeModelSheet onClose={() => setChanging(false)} /> : null}

      <BottomSheet
        title="Delete voice models?"
        open={deleting}
        onClose={() => setDeleting(false)}
        testID={voiceSettingsIds.deleteSheet}
      >
        <View style={{ gap: theme.space[2], paddingVertical: theme.space[2] }}>
          <Text
            testID={voiceSettingsIds.deleteLine}
            style={{ color: colors.text, fontSize: theme.font.size.body15 }}
          >
            {storage.deleteLine}
          </Text>
          <Button
            testID={voiceSettingsIds.deleteConfirm}
            label="Delete models"
            variant="danger"
            onPress={() => {
              setDeleting(false);
              void store.deleteAll();
            }}
          />
          <Button
            testID={voiceSettingsIds.deleteCancel}
            label="Cancel"
            variant="ghost"
            onPress={() => setDeleting(false)}
          />
        </View>
      </BottomSheet>

      {mic === null ? null : (
        <MicPermissionSheet
          open={micSheet}
          status={mic}
          onClose={() => {
            setMicSheet(false);
            refresh();
          }}
          onTypeInstead={() => setMicSheet(false)}
        />
      )}
    </ScrollView>
  );
}
