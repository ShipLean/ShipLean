import { useEffect, useState, type FunctionComponent } from 'react';
import { Platform, Text, View } from 'react-native';
import { useTheme } from '../theme';
import { Button } from '../ui/Button';
import type { ModelStatus } from '../voice/contracts';
import { engines } from '../voice/engines';
import {
  downloadView,
  noSpaceView,
  rateOf,
  timeLeft,
  withSample,
  type FileRowView,
  type NoSpaceView,
  type Sample,
} from './downloadView';
import { downloadFileRowId, downloadingStepIds } from './DownloadingStep.testIds';
import { formatGb } from './modelChoice';
import { openPhoneStorage } from './openStorage';
import { Body, Heading, ProgressLine, StepBody, StepFoot, TickedList } from './SetupParts';
import { useModelStatus } from './useModelStatus';

export interface DownloadingStepProps {
  // Use ShipLean while this downloads: on to Projects.
  onUseApp: () => void;
  // Set up voice, when nothing is downloading: back to the model screen.
  onSetUp?: () => void;
}

const PROMISES = [
  { id: 'background', title: 'Carries on if you leave the app or lock the phone' },
  { id: 'resume', title: 'Picks up where it stopped if the connection drops' },
  { id: 'mobile', title: 'Stops and asks before using mobile data' },
  { id: 'checked', title: 'Each file is checked before voice turns on' },
];

type MicState = Awaited<ReturnType<ReturnType<typeof engines>['mic']['status']>>;

const STATUS_LINE: Partial<Record<ModelStatus['phase'], string>> = {
  paused: 'paused',
  checking: 'checking the files',
  ready: 'ready',
  failed: 'stopped',
};

const REASON_LINE: Record<NonNullable<ModelStatus['reason']>, string> = {
  wifi: 'waiting for Wi-Fi',
  network: 'waiting for a connection',
  space: 'stopped',
  checksum: 'a file did not match its checksum',
  memory: 'this phone ran out of memory',
};

function FileRow({ row }: { row: FileRowView }) {
  const theme = useTheme();
  const { colors } = theme;
  const mark = { checked: '✓', downloading: '↓', waiting: '…', failed: '!', stopped: '!' }[
    row.state
  ];
  const tone = {
    checked: colors.ok,
    downloading: colors.accent,
    waiting: colors.muted,
    failed: colors.danger,
    stopped: colors.danger,
  }[row.state];
  return (
    <View
      testID={downloadFileRowId(row.model)}
      style={{
        flexDirection: 'row',
        gap: theme.space[3],
        padding: theme.space[3],
        borderRadius: theme.radius.md,
        borderWidth: 1,
        borderColor: colors.border,
        backgroundColor: colors.surface,
      }}
    >
      <Text style={{ color: tone, fontSize: theme.font.size.body15, width: 18 }}>{mark}</Text>
      <View style={{ flex: 1, gap: theme.space[1] }}>
        <View
          style={{ flexDirection: 'row', justifyContent: 'space-between', gap: theme.space[2] }}
        >
          <Text
            style={{
              color: colors.text,
              fontSize: theme.font.size.body15,
              fontWeight: theme.font.weight.semibold,
            }}
          >
            {row.name}
          </Text>
          <Text style={{ color: colors.muted, fontSize: theme.font.size.sm }}>
            {formatGb(row.bytes)}
          </Text>
        </View>
        <Text style={{ color: colors.muted, fontSize: theme.font.size.base }}>{row.detail}</Text>
        {row.state === 'downloading' || row.state === 'stopped' ? (
          <ProgressLine
            testID={`${downloadFileRowId(row.model)}-bar`}
            fraction={row.fraction}
            tone={row.state === 'stopped' ? 'danger' : 'accent'}
          />
        ) : null}
      </View>
    </View>
  );
}

function NoSpacePanel({ space }: { space: NoSpaceView }) {
  const theme = useTheme();
  const { colors } = theme;
  const store = engines().models;
  return (
    <View
      testID={downloadingStepIds.noSpace}
      style={{
        gap: theme.space[2],
        padding: theme.space[3],
        borderRadius: theme.radius.md,
        borderWidth: 1,
        borderColor: colors.danger,
        backgroundColor: colors.surface,
      }}
    >
      <Text
        style={{
          color: colors.danger,
          fontSize: theme.font.size.body15,
          fontWeight: theme.font.weight.bold,
        }}
      >
        Not enough space to finish
      </Text>
      <Text
        testID={downloadingStepIds.noSpaceBody}
        style={{ color: colors.text, fontSize: theme.font.size.body15 }}
      >
        {`${space.model} needs ${formatGb(space.neededBytes)} more, and this phone has ${formatGb(space.freeBytes)} free. What's downloaded so far is kept.`}
      </Text>
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: theme.space[2] }}>
        <Button
          testID={downloadingStepIds.tryAgain}
          label="Try again"
          onPress={() => store.download()}
        />
        <Button
          testID={downloadingStepIds.openStorage}
          label="Open phone storage"
          variant="ghost"
          onPress={() => void openPhoneStorage()}
        />
      </View>
      {space.smaller === null ? null : (
        <Button
          testID={downloadingStepIds.useSmaller}
          label={`Switch to ${space.smaller.name}`}
          variant="ghost"
          onPress={() => {
            store.choose(space.smaller?.model ?? 'auto');
            store.download();
          }}
        />
      )}
      {space.smaller === null ? null : (
        <Text style={{ color: colors.muted, fontSize: theme.font.size.base }}>
          {`${space.smaller.name} is ${formatGb(space.smaller.bytes)} and fits once the ${space.model} download is removed.`}
        </Text>
      )}
    </View>
  );
}

// The Downloading page: a page you can leave. Progress per file, the promises, and after the microphone step the
// microphone's state. The download goes on whatever the microphone answered (microphone D4).
export const DownloadingStep: FunctionComponent<DownloadingStepProps> = ({ onUseApp, onSetUp }) => {
  const theme = useTheme();
  const { colors } = theme;
  const store = engines().models;
  const status = useModelStatus();
  const [mic, setMic] = useState<MicState | null>(null);
  const [samples, setSamples] = useState<Sample[]>([]);

  useEffect(() => {
    let live = true;
    void engines()
      .mic.status()
      .then((s) => {
        if (live) setMic(s);
      });
    return () => {
      live = false;
    };
  }, []);

  // The rate comes from the progress seen while this page is open: a sample at every change the store reports.
  useEffect(
    () =>
      store.subscribe(() => {
        const now = store.status();
        setSamples((prev) =>
          now.phase === 'downloading'
            ? withSample(prev, { at: Date.now(), bytes: now.bytesDone })
            : [],
        );
      }),
    [store],
  );

  if (status.phase === 'none' || status.phase === 'skipped') {
    return (
      <>
        <StepBody testID={downloadingStepIds.root}>
          <Heading>No voice models are downloading</Heading>
          <Body testID={downloadingStepIds.idle} muted>
            Voice is off on this phone. Set it up to download the models.
          </Body>
        </StepBody>
        <StepFoot>
          <Button
            testID={downloadingStepIds.setUp}
            label="Set up voice"
            onPress={() => onSetUp?.()}
          />
          <Button
            testID={downloadingStepIds.useApp}
            label="Use ShipLean"
            variant="ghost"
            onPress={onUseApp}
          />
        </StepFoot>
      </>
    );
  }

  const view = downloadView(status);
  const space = noSpaceView(status);
  const left = timeLeft(Math.max(0, status.bytesTotal - status.bytesDone), rateOf(samples));
  const why =
    status.phase === 'ready'
      ? null
      : status.reason !== null && status.phase !== 'downloading'
        ? REASON_LINE[status.reason]
        : (STATUS_LINE[status.phase] ?? null);
  const parts = [
    view.amount,
    why ?? (left === null ? null : `${left}${status.wifiOnly ? ' on Wi-Fi' : ''}`),
  ].filter((p): p is string => p !== null);
  const running = status.phase === 'downloading' || status.phase === 'checking';
  const stoppedForSpace = space !== null;

  return (
    <>
      <StepBody testID={downloadingStepIds.root}>
        <Text
          testID={downloadingStepIds.percent}
          style={{
            color: stoppedForSpace ? colors.danger : colors.text,
            fontSize: 40,
            fontWeight: theme.font.weight.bold,
          }}
        >
          {`${view.percent}%`}
        </Text>
        <ProgressLine
          testID={downloadingStepIds.bar}
          fraction={view.percent / 100}
          tone={stoppedForSpace ? 'danger' : 'accent'}
          height={10}
        />
        <Text
          testID={downloadingStepIds.amount}
          style={{ color: colors.muted, fontSize: theme.font.size.base }}
        >
          {parts.join(' · ')}
        </Text>

        {space === null ? null : <NoSpacePanel space={space} />}

        {mic === 'deniedCanAsk' || mic === 'deniedFinal' ? (
          <View
            testID={downloadingStepIds.micOff}
            style={{
              gap: theme.space[2],
              padding: theme.space[3],
              borderRadius: theme.radius.md,
              borderWidth: 1,
              borderColor: colors.warn,
              backgroundColor: colors.surface,
            }}
          >
            <Text
              style={{
                color: colors.text,
                fontSize: theme.font.size.body15,
                fontWeight: theme.font.weight.bold,
              }}
            >
              Microphone is off
            </Text>
            <Text style={{ color: colors.text, fontSize: theme.font.size.body15 }}>
              Voice will be ready, but it can't hear you until the microphone is on. Typed commands
              still work.
            </Text>
            <Button
              testID={downloadingStepIds.micOffSettings}
              label="Open Settings"
              variant="ghost"
              onPress={() => engines().mic.openSettings()}
            />
          </View>
        ) : null}
        {mic === 'undetermined' ? (
          <View
            testID={downloadingStepIds.micNotAsked}
            style={{
              padding: theme.space[3],
              borderRadius: theme.radius.md,
              borderWidth: 1,
              borderStyle: 'dashed',
              borderColor: colors.border,
            }}
          >
            <Text style={{ color: colors.muted, fontSize: theme.font.size.base }}>
              Microphone: not asked yet. We'll ask the first time you hold the mic.
            </Text>
          </View>
        ) : null}

        <View testID={downloadingStepIds.files} style={{ gap: theme.space[2] }}>
          {view.rows.map((row) => (
            <FileRow key={row.model} row={row} />
          ))}
        </View>

        <TickedList lines={PROMISES} testID={downloadingStepIds.promises} />
        {Platform.OS === 'ios' ? (
          <Text
            testID={downloadingStepIds.keepOpen}
            style={{ color: colors.muted, fontSize: theme.font.size.base }}
          >
            Keep ShipLean open on iPhone to finish faster
          </Text>
        ) : null}
      </StepBody>
      <StepFoot>
        <Button
          testID={downloadingStepIds.useApp}
          label={
            stoppedForSpace
              ? 'Use ShipLean while you sort this out'
              : 'Use ShipLean while this downloads'
          }
          onPress={onUseApp}
        />
        {running ? (
          <Button
            testID={downloadingStepIds.pause}
            label="Pause download"
            variant="ghost"
            onPress={() => store.pause()}
          />
        ) : status.phase === 'paused' ? (
          <Button
            testID={downloadingStepIds.resume}
            label="Resume download"
            variant="ghost"
            onPress={() => store.download()}
          />
        ) : null}
      </StepFoot>
    </>
  );
};
