import type { BacklogSectionDto } from '../../../api/views';

/** Which Planning sections show their rows. */
export interface SectionCollapse {
  isOpen: (sectionId: string) => boolean;
  toggle: (sectionId: string) => void;
  /** The collapse while a search is on: sections with hits open, without saving that. Same collapse when off. */
  withSearch: (
    searchActive: boolean,
    filteredSections: readonly BacklogSectionDto[],
  ) => SectionCollapse;
}

const noop = () => undefined;
const allOpen: SectionCollapse = {
  isOpen: () => true,
  toggle: noop,
  withSearch: () => allOpen,
};

/** Collapsible sections; the running sprint starts closed. */
// TODO: the open/closed state and the running sprint's default replace this all-open stand-in.
export function useSectionCollapse(sections: readonly BacklogSectionDto[]): SectionCollapse {
  void sections;
  return allOpen;
}
