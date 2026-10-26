import { ProjectKeyBadge } from '../../shell/ProjectKeyBadge';
import { Match } from './Match';

export function ProjectRow({
  projectKey,
  name,
  detail,
  current,
  query,
}: {
  projectKey: string;
  name: string;
  detail: string;
  current: boolean;
  query: string;
}) {
  return (
    <>
      <span aria-hidden="true" className="text-center text-ink-muted">
        ⌂
      </span>
      <span className="min-w-0 truncate">
        <Match text={name} query={query} />
      </span>
      <span className="flex items-center justify-end gap-2 text-[length:var(--text-xs)] text-ink-muted">
        <span>{detail}</span>
        <ProjectKeyBadge current={current}>{projectKey}</ProjectKeyBadge>
      </span>
    </>
  );
}
