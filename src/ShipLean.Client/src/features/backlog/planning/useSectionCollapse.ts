import { useQuery } from '@tanstack/react-query';
import { useEffect, useMemo, useRef } from 'react';
import type { BacklogSectionDto } from '../../../api/views';
import { showToast } from '../../../components/toastStore';
import {
  BACKLOG_COLUMNS,
  backlogViewKey,
  useBacklogView,
  type BacklogViewPreference,
} from '../backlogApi';

/** Which Planning sections show their rows. */
export interface SectionCollapse {
  isOpen: (sectionId: string) => boolean;
  toggle: (sectionId: string) => void;
  /** Opens a closed section (saved like a toggle); an open one stays as it is. */
  open: (sectionId: string) => void;
  /** A search is on, so the sections follow its hits and cannot be toggled. */
  searching: boolean;
  /** The collapse while a search is on: sections with hits open, without saving that. Same collapse when off. */
  withSearch: (
    searchActive: boolean,
    filteredSections: readonly BacklogSectionDto[],
  ) => SectionCollapse;
}

/**
 * Collapsible sections, kept in the saved `view:backlog` preference as `collapsedSections`. With nothing saved the
 * running sprint is closed: you plan what is next. The first toggle saves every section's state, so the running sprint
 * stays closed unless it was opened. The preference is the signed-in user's across projects, so each entry is
 * `<projectId>:<sectionId>`; a save keeps the other projects' entries.
 */
export function useSectionCollapse(
  projectId: string,
  sections: readonly BacklogSectionDto[],
): SectionCollapse {
  const { update } = useBacklogView('stories', BACKLOG_COLUMNS);
  // Reads the preference useBacklogView fetched; only the cache is watched here.
  const preference = useQuery({ queryKey: backlogViewKey, enabled: false });
  const saved = (preference.data as BacklogViewPreference | null | undefined)?.collapsedSections;
  const save = useRef(update);
  useEffect(() => {
    save.current = update;
  });
  return useMemo(() => {
    const closed = (id: string) =>
      saved === undefined
        ? sections.some((s) => s.id === id && s.sprint?.status === 'current')
        : saved.includes(`${projectId}:${id}`);
    const base: SectionCollapse = {
      isOpen: (id) => !closed(id),
      toggle: (id) => {
        const mine = sections
          .filter((s) => (s.id === id ? !closed(id) : closed(s.id)))
          .map((s) => `${projectId}:${s.id}`);
        const others = (saved ?? []).filter((entry) => !entry.startsWith(`${projectId}:`));
        save.current({ collapsedSections: [...others, ...mine] }, (error) =>
          showToast(`Could not save which sections are open: ${error.message}`, { tone: 'danger' }),
        );
      },
      open: (id) => {
        if (closed(id)) base.toggle(id);
      },
      searching: false,
      withSearch: (searchActive, filtered) =>
        searchActive
          ? {
              isOpen: (id) => filtered.some((s) => s.id === id && s.rows.length > 0),
              toggle: () => undefined,
              open: () => undefined,
              searching: true,
              withSearch: base.withSearch,
            }
          : base,
    };
    return base;
  }, [saved, sections, projectId]);
}
