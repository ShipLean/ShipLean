import { useSession } from '../session/useSession';
import { Screen } from '../ui/Screen';
import { TextField } from '../ui/TextField';
import { screenIds } from './testIds';

// TODO: placeholder; the Sign in screen (logo, server field with its check, browser handoff, app version) replaces it.
export function SignInScreen() {
  const { origin } = useSession();
  return (
    <Screen testID={screenIds.signIn}>
      <TextField
        label="Server"
        value={origin ?? ''}
        onChangeText={() => undefined}
        testID={screenIds.signInServerField}
      />
    </Screen>
  );
}
