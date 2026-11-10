import { StackPlaceholder } from '../shell/StackPlaceholder';
import { screenIds } from './testIds';

// TODO: the voice models' licences replace this placeholder.
export function LicencesScreen() {
  return <StackPlaceholder title="Licences" testID={screenIds.licences} />;
}
