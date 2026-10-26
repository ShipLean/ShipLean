import { useSignInMode } from '../../../auth/useSignInMode';
import { settingsTestIds } from '../../testIds';
import { AppearancePanel } from './AppearancePanel';
import { MyProjectsPanel } from './MyProjectsPanel';
import { PasswordPanel } from './PasswordPanel';
import { SessionsPanel } from './SessionsPanel';

/** The account half of Your profile: password, appearance, your projects, sessions and saved views. */
export function ProfileAccount() {
  const { proxy } = useSignInMode();
  return (
    <div data-testid={settingsTestIds.accountPanel} className="grid content-start gap-3">
      {proxy ? null : <PasswordPanel />}
      <AppearancePanel />
      <MyProjectsPanel />
      <SessionsPanel />
    </div>
  );
}
