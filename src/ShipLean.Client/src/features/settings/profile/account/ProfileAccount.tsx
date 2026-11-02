import { useQuery } from '@tanstack/react-query';
import { fetchMe, meKey } from '../../../../queries/shared';
import { useSignInMode } from '../../../auth/useSignInMode';
import { settingsTestIds } from '../../testIds';
import { AppearancePanel } from './AppearancePanel';
import { MyProjectsPanel } from './MyProjectsPanel';
import { PasswordPanel } from './PasswordPanel';
import { SessionsPanel } from './SessionsPanel';

/** The account half of Your profile: password, appearance, your projects, sessions and saved views. */
export function ProfileAccount() {
  const { proxy } = useSignInMode();
  const status = useQuery({ queryKey: meKey, queryFn: fetchMe }).data?.status;
  // With only Google there is no current password to give, so the form could never succeed.
  const googleOnly = status === 'googleOnly';
  return (
    <div data-testid={settingsTestIds.accountPanel} className="grid content-start gap-3">
      {proxy || googleOnly ? null : <PasswordPanel />}
      <AppearancePanel />
      <MyProjectsPanel />
      <SessionsPanel />
    </div>
  );
}
