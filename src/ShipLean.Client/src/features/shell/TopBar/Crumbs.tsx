import { useLocation, useParams } from 'react-router-dom';
import { crumbTrail, matchRoute } from '../../../routes';
import { shellTestIds as ids } from '../testIds';

// Where you are: the project key (or ShipLean on install-level pages), then the screen's crumbs. An address that is
// no screen shows just the root.
export function Crumbs() {
  const { pathname, search } = useLocation();
  const key = useParams().key;
  const route = matchRoute(pathname, search);
  const trail = route
    ? crumbTrail(route.id, route.params as never, route.query)
    : [key ?? 'ShipLean'];
  return (
    <nav
      data-testid={ids.crumbs}
      aria-label="You are here"
      className="flex min-w-0 items-center gap-1.5 text-[length:var(--text-sm)] text-ink-muted"
    >
      {trail.map((crumb, index) => (
        <span key={`${index}-${crumb}`} className="flex items-center gap-1.5">
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
