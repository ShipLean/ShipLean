import { useId } from 'react';
import { settingsDialogTestIds as ids } from '../../features/settingsDialog/testIds';
import { isDisabledPane, type PaneDef, type PaneGroup } from './types';

export function SettingsNav<TDoc, TContext>({
  groups,
  activeId,
  dirtyIds,
  warnings,
  onSelect,
}: {
  groups: PaneGroup<TDoc, TContext>[];
  activeId: string | undefined;
  dirtyIds: ReadonlySet<string>;
  warnings: ReadonlyMap<string, string>;
  onSelect(id: string): void;
}) {
  return (
    <nav data-testid={ids.nav} aria-label="Settings sections" className="grid content-start gap-1">
      {groups.map((group) => (
        <NavGroup
          key={group.label}
          group={group}
          activeId={activeId}
          dirtyIds={dirtyIds}
          warnings={warnings}
          onSelect={onSelect}
        />
      ))}
    </nav>
  );
}

function NavGroup<TDoc, TContext>({
  group,
  activeId,
  dirtyIds,
  warnings,
  onSelect,
}: {
  group: PaneGroup<TDoc, TContext>;
  activeId: string | undefined;
  dirtyIds: ReadonlySet<string>;
  warnings: ReadonlyMap<string, string>;
  onSelect(id: string): void;
}) {
  const labelId = useId();
  return (
    <div className="grid gap-px pb-2">
      <div
        id={labelId}
        className="px-2 py-1 text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase"
      >
        {group.label}
      </div>
      <ul aria-labelledby={labelId} className="m-0 grid list-none gap-px p-0">
        {group.panes.map((pane) => (
          <NavItem
            key={pane.id}
            pane={pane}
            active={pane.id === activeId}
            dirty={dirtyIds.has(pane.id)}
            warning={warnings.get(pane.id)}
            onSelect={onSelect}
          />
        ))}
      </ul>
    </div>
  );
}

function NavItem<TDoc, TContext>({
  pane,
  active,
  dirty,
  warning,
  onSelect,
}: {
  pane: PaneDef<TDoc, TContext>;
  active: boolean;
  dirty: boolean;
  warning: string | undefined;
  onSelect(id: string): void;
}) {
  const disabled = isDisabledPane(pane);
  const hintId = useId();
  return (
    <li>
      <button
        type="button"
        data-testid={`${ids.navItem}-${pane.id}`}
        data-dirty={dirty}
        aria-current={active ? 'page' : undefined}
        disabled={disabled}
        aria-describedby={disabled ? hintId : undefined}
        onClick={() => onSelect(pane.id)}
        className={`flex h-8 w-full items-center gap-2 rounded-control px-2 text-left text-[length:var(--text-base)] disabled:text-ink-muted ${
          active ? 'bg-signal-soft font-semibold text-ink' : 'text-ink hover:bg-sunken'
        }`}
      >
        <span className="flex-1">{pane.label}</span>
        {warning === undefined ? null : (
          <span
            data-testid={`${ids.navWarning}-${pane.id}`}
            title={warning}
            className="text-caution"
          >
            <span aria-hidden="true">!</span>
            <span className="sr-only">has warnings</span>
          </span>
        )}
        {dirty ? (
          <span data-testid={`${ids.navDot}-${pane.id}`} title="Unsaved changes">
            <span aria-hidden="true" className="block size-[7px] rounded-full bg-signal" />
            <span className="sr-only">unsaved changes</span>
          </span>
        ) : null}
      </button>
      {disabled ? (
        <div
          id={hintId}
          data-testid={`${ids.navDisabledHint}-${pane.id}`}
          className="px-2 pb-1 text-[length:var(--text-xs)] text-ink-muted"
        >
          {pane.disabled}
        </div>
      ) : null}
    </li>
  );
}
