import { View } from 'react-native';
import { useTheme } from '../../theme';

export interface LevelMeterProps {
  // 0..1 on the -60..0 dBFS scale.
  level: number;
  // RMS values (16-bit scale) for the markers; each is placed on the same scale.
  lineRms?: number;
  roomRms?: number;
  voiceRms?: number;
  testID?: string;
}

const clamp = (n: number) => Math.min(1, Math.max(0, n));

// An RMS on the meter's -60..0 dBFS scale, 0..1.
export function rmsToLevel(rms: number): number {
  if (rms <= 0) return 0;
  return clamp((20 * Math.log10(rms / 32768) + 60) / 60);
}

// A bar: a track, the live level, and markers for the speech line, the room and the voice.
export function LevelMeter({ level, lineRms, roomRms, voiceRms, testID }: LevelMeterProps) {
  const { colors } = useTheme();
  const marker = (rms: number | undefined, color: string, id: string) =>
    rms === undefined ? null : (
      <View
        key={id}
        testID={testID === undefined ? undefined : `${testID}-${id}`}
        style={{
          position: 'absolute',
          top: -8,
          bottom: -8,
          width: 2,
          left: `${rmsToLevel(rms) * 100}%`,
          backgroundColor: color,
        }}
      />
    );
  return (
    <View
      testID={testID}
      accessibilityRole="progressbar"
      accessibilityValue={{ min: 0, max: 100, now: Math.round(clamp(level) * 100) }}
      style={{
        height: 16,
        borderRadius: 8,
        backgroundColor: colors.surface2,
        borderWidth: 1,
        borderColor: colors.border,
      }}
    >
      <View
        testID={testID === undefined ? undefined : `${testID}-fill`}
        style={{
          width: `${clamp(level) * 100}%`,
          height: '100%',
          borderRadius: 8,
          backgroundColor: colors.accent,
        }}
      />
      {marker(roomRms, colors.muted, 'room')}
      {marker(voiceRms, colors.ok, 'voice')}
      {marker(lineRms, colors.text, 'line')}
    </View>
  );
}
