import { useState, type FunctionComponent, type ReactNode } from 'react';
import { Pressable, Switch, Text, View } from 'react-native';
import { useTheme } from '../theme';
import { Button } from '../ui/Button';
import type { ModelChoice } from '../voice/contracts';
import { engines } from '../voice/engines';
import { chosenModel, formatGb, modelChoiceView, totalBytes, type CommandRow } from './modelChoice';
import { modelOptionId, modelsStepIds } from './ModelsStep.testIds';
import { Body, GroupHeading, Heading, ProgressLine, StepBody, StepFoot } from './SetupParts';
import { useModelStatus } from './useModelStatus';

export interface ModelsStepProps {
  // Download and continue: the choice is made and the download started.
  onContinue: () => void;
  // Skip voice for now, or Continue on a phone with no voice.
  onSkip: () => void;
}

interface OptionProps {
  id: ModelChoice;
  selected: boolean;
  disabled?: boolean;
  onPress: () => void;
  name: string;
  tag?: string;
  size?: string;
  recommended?: boolean;
  children?: ReactNode;
}

function Option({
  id,
  selected,
  disabled,
  onPress,
  name,
  tag,
  size,
  recommended,
  children,
}: OptionProps) {
  const theme = useTheme();
  const { colors } = theme;
  return (
    <Pressable
      testID={modelOptionId(id)}
      accessibilityRole="radio"
      accessibilityLabel={name}
      accessibilityState={{ selected, disabled: disabled === true }}
      disabled={disabled}
      onPress={onPress}
      style={{
        minHeight: theme.touch.row,
        flexDirection: 'row',
        gap: theme.space[3],
        padding: theme.space[3],
        borderRadius: theme.radius.md,
        borderWidth: 1,
        borderColor: selected ? colors.accent : colors.border,
        backgroundColor: selected ? colors.accentSoft : colors.surface,
        opacity: disabled === true ? 0.55 : 1,
      }}
    >
      <View
        style={{
          width: 20,
          height: 20,
          marginTop: 2,
          borderRadius: 10,
          borderWidth: 2,
          borderColor: selected ? colors.accent : colors.muted,
          alignItems: 'center',
          justifyContent: 'center',
        }}
      >
        {selected ? (
          <View
            style={{ width: 10, height: 10, borderRadius: 5, backgroundColor: colors.accent }}
          />
        ) : null}
      </View>
      <View style={{ flex: 1, gap: theme.space[1] }}>
        <View
          style={{
            flexDirection: 'row',
            flexWrap: 'wrap',
            alignItems: 'center',
            gap: theme.space[2],
          }}
        >
          <Text
            style={{
              color: colors.text,
              fontSize: theme.font.size.body15,
              fontWeight: theme.font.weight.semibold,
            }}
          >
            {name}
          </Text>
          {recommended === true ? (
            <Text
              style={{
                color: colors.accent,
                fontSize: theme.font.size.sm,
                fontWeight: theme.font.weight.semibold,
              }}
            >
              Recommended
            </Text>
          ) : null}
          {tag === undefined ? null : (
            <Text style={{ color: colors.muted, fontSize: theme.font.size.sm }}>{tag}</Text>
          )}
          {size === undefined ? null : (
            <Text style={{ color: colors.muted, fontSize: theme.font.size.sm, marginLeft: 'auto' }}>
              {size}
            </Text>
          )}
        </View>
        {children}
      </View>
    </Pressable>
  );
}

function CommandOption({
  row,
  selected,
  onPress,
}: {
  row: CommandRow;
  selected: boolean;
  onPress: () => void;
}) {
  const theme = useTheme();
  const blocked = row.availability === 'blocked';
  return (
    <Option
      id={row.id}
      selected={selected}
      disabled={blocked}
      onPress={onPress}
      name={row.name}
      tag={row.tag}
      size={formatGb(row.bytes)}
    >
      {blocked && row.blockedReason !== null ? (
        <Text
          testID={modelsStepIds.blocked4}
          style={{ color: theme.colors.muted, fontSize: theme.font.size.base }}
        >
          {row.blockedReason}
        </Text>
      ) : (
        <Text style={{ color: theme.colors.muted, fontSize: theme.font.size.base }}>
          {row.detail}
        </Text>
      )}
      {row.warning === null ? null : (
        <Text
          testID={modelsStepIds.warning4}
          style={{ color: theme.colors.warn, fontSize: theme.font.size.base }}
        >
          {row.warning}
        </Text>
      )}
    </Option>
  );
}

// Step 2: Auto is selected and says what it picked for this phone; the download size, the free space and Wi-Fi
// only are on this page; nothing downloads until Download and continue.
export const ModelsStep: FunctionComponent<ModelsStepProps> = ({ onContinue, onSkip }) => {
  const theme = useTheme();
  const { colors } = theme;
  const store = engines().models;
  const status = useModelStatus();
  const view = modelChoiceView(store.deviceMemory());
  const [pick, setPick] = useState<ModelChoice>('auto');

  if (!view.voice) {
    return (
      <>
        <StepBody testID={modelsStepIds.root}>
          <Heading>Voice isn't available on this phone</Heading>
          <Body testID={modelsStepIds.noVoice}>{view.noVoiceReason}</Body>
        </StepBody>
        <StepFoot>
          <Button
            testID={modelsStepIds.noVoiceContinue}
            label="Continue"
            onPress={() => {
              store.skip();
              onSkip();
            }}
          />
        </StepFoot>
      </>
    );
  }

  const bytes = totalBytes(view, pick) ?? 0;
  const share = status.freeBytes > 0 ? bytes / status.freeBytes : 1;
  const start = () => {
    // choose() doesn't start a download from none or skipped, so the screen asks for it.
    store.choose(pick);
    store.download();
    onContinue();
  };
  const picked = chosenModel(view, pick);

  return (
    <>
      <StepBody testID={modelsStepIds.root}>
        <Heading>Choose your voice models</Heading>
        <Body muted>
          Voice runs on this phone, so it needs two models. You can change them later.
        </Body>

        <GroupHeading>Command model · Qwen3</GroupHeading>
        <View style={{ gap: theme.space[2] }}>
          <Option
            id="auto"
            selected={pick === 'auto'}
            onPress={() => setPick('auto')}
            name="Auto"
            recommended
          >
            {view.auto === null ? null : (
              <Text
                testID={modelsStepIds.autoPick}
                style={{ color: colors.text, fontSize: theme.font.size.base }}
              >
                {view.auto.line}
              </Text>
            )}
          </Option>
          {view.commandRows.map((row) => (
            <CommandOption
              key={row.id}
              row={row}
              selected={pick === row.id}
              onPress={() => setPick(row.id)}
            />
          ))}
        </View>
        <Text
          testID={modelsStepIds.estimateNote}
          style={{ color: colors.muted, fontSize: theme.font.size.sm }}
        >
          {view.estimateNote}
        </Text>

        <GroupHeading>Speech to text · Same on every phone</GroupHeading>
        <View
          testID={modelsStepIds.speech}
          style={{
            minHeight: theme.touch.row,
            flexDirection: 'row',
            alignItems: 'center',
            gap: theme.space[3],
            padding: theme.space[3],
            borderRadius: theme.radius.md,
            borderWidth: 1,
            borderColor: colors.border,
            backgroundColor: colors.surface,
          }}
        >
          <Text style={{ color: colors.ok, fontSize: theme.font.size.body15 }}>✓</Text>
          <Text
            style={{
              flex: 1,
              color: colors.text,
              fontSize: theme.font.size.body15,
              fontWeight: theme.font.weight.semibold,
            }}
          >
            {view.speech.name}
          </Text>
          <Text style={{ color: colors.muted, fontSize: theme.font.size.sm }}>
            {formatGb(view.speech.bytes)}
          </Text>
        </View>

        <View
          style={{
            gap: theme.space[2],
            padding: theme.space[3],
            borderRadius: theme.radius.md,
            backgroundColor: colors.surface,
            borderWidth: 1,
            borderColor: colors.border,
          }}
        >
          <View
            style={{ flexDirection: 'row', justifyContent: 'space-between', gap: theme.space[2] }}
          >
            <Text
              testID={modelsStepIds.total}
              style={{
                color: colors.text,
                fontSize: theme.font.size.body15,
                fontWeight: theme.font.weight.bold,
              }}
            >
              {picked === null ? 'Choose a model' : `Download ${formatGb(bytes)}`}
            </Text>
            <Text
              testID={modelsStepIds.free}
              style={{ color: colors.muted, fontSize: theme.font.size.base }}
            >
              {formatGb(status.freeBytes)} free on this phone
            </Text>
          </View>
          <ProgressLine testID={modelsStepIds.freeBar} fraction={share} />
          <View
            style={{
              flexDirection: 'row',
              alignItems: 'center',
              gap: theme.space[2],
              minHeight: theme.touch.control,
            }}
          >
            <View style={{ flex: 1 }}>
              <Text
                style={{
                  color: colors.text,
                  fontSize: theme.font.size.body15,
                  fontWeight: theme.font.weight.semibold,
                }}
              >
                Wi-Fi only
              </Text>
              <Text style={{ color: colors.muted, fontSize: theme.font.size.base }}>
                Ask before using mobile data
              </Text>
            </View>
            <Switch
              testID={modelsStepIds.wifiOnly}
              accessibilityLabel="Wi-Fi only"
              value={status.wifiOnly}
              onValueChange={(on) => store.setWifiOnly(on)}
            />
          </View>
        </View>
      </StepBody>
      <StepFoot>
        <Button testID={modelsStepIds.download} label="Download and continue" onPress={start} />
        <Button
          testID={modelsStepIds.skip}
          label="Skip voice for now"
          variant="ghost"
          onPress={() => {
            store.skip();
            onSkip();
          }}
        />
      </StepFoot>
    </>
  );
};
