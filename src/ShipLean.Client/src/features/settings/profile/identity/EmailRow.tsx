import { settingsTestIds } from '../../testIds';
import { FormRow } from './FormRow';

export function EmailRow({ email, proxy }: { email: string; proxy: boolean }) {
  return (
    <FormRow label="Email">
      <span
        data-testid={settingsTestIds.profileEmail}
        className="inline-flex h-7 items-center rounded-control border border-dashed border-edge bg-canvas px-2.5 text-[length:var(--text-sm)] text-ink-muted touch:block touch:h-9 touch:max-w-full touch:truncate touch:text-[length:var(--text-base)] touch:leading-[34px]"
      >
        {email}
      </span>
      <span
        data-testid={proxy ? settingsTestIds.profileEmailSource : undefined}
        className="text-[length:var(--text-xs)] text-ink-muted touch:text-[length:var(--text-sm)]"
      >
        {proxy
          ? 'Your email comes from your sign-in provider.'
          : 'You sign in with this. Only an instance admin can change it.'}
      </span>
    </FormRow>
  );
}
