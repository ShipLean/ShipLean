import { ModelLicences } from '../setup/ModelLicences';
import { Screen } from '../ui/Screen';
import { TopBar } from '../ui/TopBar';
import { SettingsBack } from './SettingsBack';
import { screenIds } from './testIds';

export function LicencesScreen() {
  return (
    <Screen testID={screenIds.licences}>
      <TopBar title="Model licences" left={<SettingsBack />} />
      <ModelLicences />
    </Screen>
  );
}
