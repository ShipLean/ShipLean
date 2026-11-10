import { Redirect, useLocalSearchParams } from 'expo-router';
import { isSetupStep, links } from '../../src/links';
import { SetupScreen } from '../../src/screens/SetupScreen';

export default function SetupRoute() {
  const { step } = useLocalSearchParams<{ step: string }>();
  // A step the app doesn't know (an old or mistyped link) goes to Projects.
  return isSetupStep(step) ? <SetupScreen step={step} /> : <Redirect href={links.projects()} />;
}
