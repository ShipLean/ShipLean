import { useQuery } from '@tanstack/react-query';
import { fetchMe, meKey } from '../../../../queries/shared';
import { settingsTestIds } from '../../testIds';
import { IdentityForm } from './IdentityForm';

export function ProfileIdentity() {
  const me = useQuery({ queryKey: meKey, queryFn: fetchMe });
  if (me.data === undefined) {
    return (
      <p
        data-testid={me.isError ? settingsTestIds.loadFailed : settingsTestIds.loading}
        className="p-4 text-ink-muted"
      >
        {me.isError ? 'Your profile could not be loaded.' : 'Loading…'}
      </p>
    );
  }
  return <IdentityForm key={me.data.id} me={me.data} />;
}
