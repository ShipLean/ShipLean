import { useQuery } from '@tanstack/react-query';
import { useLayoutEffect, useRef, useState } from 'react';
import { useLocation, useParams } from 'react-router-dom';
import { projectByKeyQuery, sprintsQuery } from '../../../pages/projectQueries';
import { crumbTrail, matchRoute } from '../../../routes';
import { useTouchShell } from '../../../touch/useTouchShell';
import { attachmentMarkdownKey, fetchAttachmentMarkdown } from '../../feature/featureApi';
import { shellTestIds as ids } from '../testIds';

// At touch widths the trail stays on one line; when it doesn't fit it is cut from the left (aligned to the end, so the
// overflow is on the left) behind a 20px fade, so the screen's name always shows (SHIP-713).
const FADE = '[mask-image:linear-gradient(to_right,transparent,black_20px)]';

function useCutFromLeft(touch: boolean, trail: readonly string[]) {
  const nav = useRef<HTMLElement>(null);
  const [cut, setCut] = useState(false);
  const signature = trail.join('\n');
  useLayoutEffect(() => {
    const element = nav.current;
    if (!touch || element === null) return undefined;
    // Overflowing on the right while aligned to the start, or on the left once aligned to the end.
    const measure = () => {
      const box = element.getBoundingClientRect();
      const first = element.firstElementChild?.getBoundingClientRect();
      const last = element.lastElementChild?.getBoundingClientRect();
      setCut(
        first !== undefined &&
          last !== undefined &&
          (first.left < box.left - 0.5 || last.right > box.right + 0.5),
      );
    };
    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(element);
    return () => observer.disconnect();
  }, [touch, signature]);
  return { nav, cut: touch && cut };
}

// Where you are: the project key (or ShipLean on install-level pages), then the screen's crumbs. An address that is
// no screen shows just the root.
export function Crumbs() {
  const { pathname, search } = useLocation();
  const key = useParams().key;
  const route = matchRoute(pathname, search);
  const { touch } = useTouchShell();
  // The sprint Retro screen names its sprint ("Sprint 24"), as the design does; the other sprint tabs still say
  // "Sprint". The project and its sprints are the queries the page itself makes, so this reads the cache.
  const sprintId =
    route?.id === 'sprintRetro' ? (route.params as { sprintId: string }).sprintId : undefined;
  const project = useQuery({ ...projectByKeyQuery(key ?? ''), enabled: sprintId !== undefined });
  const sprints = useQuery({
    ...sprintsQuery(project.data?.id ?? ''),
    enabled: sprintId !== undefined && project.data !== undefined,
  });
  const sprintName = sprints.data?.find((s) => s.id === sprintId)?.name;
  // On a phone the attachment page's last crumb is the file's name (SHIP-715 D8), read from the page's own query. The
  // desktop crumb keeps saying "Attachment".
  const attachmentParams =
    touch && route?.id === 'workItemAttachment'
      ? (route.params as { key: string; attachmentId: string })
      : undefined;
  const attachmentId = attachmentParams?.attachmentId;
  const attachmentProject = useQuery({
    ...projectByKeyQuery(attachmentParams?.key ?? ''),
    enabled: attachmentId !== undefined,
  });
  const attachmentProjectId = attachmentProject.data?.id ?? '';
  const attachment = useQuery({
    queryKey: attachmentMarkdownKey(attachmentProjectId, attachmentId ?? ''),
    queryFn: () => fetchAttachmentMarkdown(attachmentId ?? ''),
    enabled: attachmentId !== undefined && attachmentProject.data !== undefined,
  });
  const fileName = attachmentId === undefined ? undefined : attachment.data?.attachment.name;
  const routeTrail = route
    ? crumbTrail(route.id, route.params as never, route.query)
    : [key ?? 'ShipLean'];
  const trail = routeTrail.map((crumb, index) => {
    if (index === 2 && sprintName !== undefined) return sprintName;
    if (index === routeTrail.length - 1 && fileName !== undefined) return fileName;
    return crumb;
  });
  const { nav, cut } = useCutFromLeft(touch, trail);
  return (
    <nav
      ref={nav}
      data-testid={ids.crumbs}
      data-cut={touch ? cut : undefined}
      aria-label="You are here"
      className={`flex min-w-0 items-center gap-1.5 text-[length:var(--text-sm)] text-ink-muted touch:flex-1 ${cut ? `justify-end ${FADE}` : ''}`}
    >
      {trail.map((crumb, index) => (
        <span key={`${index}-${crumb}`} className="flex items-center gap-1.5 touch:flex-none">
          {index > 0 ? <span aria-hidden="true">›</span> : null}
          <span
            data-testid={ids.crumb}
            className={index === trail.length - 1 ? 'font-semibold text-ink' : undefined}
            aria-current={index === trail.length - 1 ? 'page' : undefined}
          >
            {crumb}
          </span>
        </span>
      ))}
    </nav>
  );
}
