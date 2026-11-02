import { useQuery } from '@tanstack/react-query';
import { useEffect, useMemo, useRef } from 'react';
import type { BacklogSectionDto } from '../../../api/views';
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
 * stays closed unless it was opened.
 */
export function useSectionCollapse(sections: readonly BacklogSectionDto[]): SectionCollapse {
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
        : saved.includes(id);
    const base: SectionCollapse = {
      isOpen: (id) => !closed(id),
      toggle: (id) => {
        const next = sections
          .filter((s) => (s.id === id ? !closed(id) : closed(s.id)))
          .map((s) => s.id);
        save.current({ collapsedSections: next });
      },
      searching: false,
      withSearch: (searchActive, filtered) =>
        searchActive
          ? {
              isOpen: (id) => filtered.some((s) => s.id === id && s.rows.length > 0),
              toggle: () => undefined,
              searching: true,
              withSearch: base.withSearch,
            }
          : base,
    };
    return base;
  }, [saved, sections]);
}
