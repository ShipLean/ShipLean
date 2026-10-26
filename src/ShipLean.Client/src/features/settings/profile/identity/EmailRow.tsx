import { settingsTestIds } from '../../testIds';
import { FormRow } from './FormRow';

export function EmailRow({ email, proxy }: { email: string; proxy: boolean }) {
  return (
    <FormRow label="Email">
      <span
        data-testid={settingsTestIds.profileEmail}
        className="inline-flex h-7 items-center rounded-control border border-dashed border-edge bg-canvas px-2.5 text-[length:var(--text-sm)] text-ink-muted"
      >
        {email}
      </span>
      <span
        data-testid={proxy ? settingsTestIds.profileEmailSource : undefined}
        className="text-[length:var(--text-xs)] text-ink-muted"
      >
        {proxy
          ? 'Your email comes from your sign-in provider.'
          : 'You sign in with this. Only an instance admin can change it.'}
      </span>
    </FormRow>
  );
}
