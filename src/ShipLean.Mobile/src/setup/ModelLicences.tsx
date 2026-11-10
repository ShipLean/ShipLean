import { openBrowserAsync } from 'expo-web-browser';
import { ScrollView, Text, View } from 'react-native';
import { useTheme } from '../theme';
import { licenceEntryId, licenceLinkId, modelLicencesIds } from './ModelLicences.testIds';
import { licenceEntries, licenceLabel } from './voiceSettingsView';

// The attribution for every model the app downloads (D9), one entry per model, straight from the pinned manifest.
export function ModelLicences() {
  const theme = useTheme();
  const { colors } = theme;
  const line = { color: colors.muted, fontSize: theme.font.size.base } as const;
  return (
    <ScrollView
      testID={modelLicencesIds.root}
      contentContainerStyle={{ padding: theme.space[4], gap: theme.space[3] }}
    >
      {licenceEntries().map((e) => (
        <View
          key={e.model}
          testID={licenceEntryId(e.model)}
          style={{
            gap: theme.space[1],
            padding: theme.space[3],
            borderRadius: theme.radius.md,
            borderWidth: 1,
            borderColor: colors.border,
            backgroundColor: colors.surface,
          }}
        >
          <Text
            style={{
              color: colors.text,
              fontSize: theme.font.size.body15,
              fontWeight: theme.font.weight.semibold,
            }}
          >
            {e.name}
          </Text>
          <Text style={line}>{`By ${e.author}`}</Text>
          <Text style={line}>{`Licence: ${licenceLabel(e.licence)}`}</Text>
          <Text style={line}>{`Changes: ${e.changes}`}</Text>
          {e.link === '' ? null : (
            <Text
              testID={licenceLinkId(e.model)}
              accessibilityRole="link"
              onPress={() => void openBrowserAsync(e.link)}
              style={{
                color: colors.accent,
                fontSize: theme.font.size.base,
                minHeight: theme.touch.control,
                textAlignVertical: 'center',
              }}
            >
              {e.link}
            </Text>
          )}
        </View>
      ))}
    </ScrollView>
  );
}
