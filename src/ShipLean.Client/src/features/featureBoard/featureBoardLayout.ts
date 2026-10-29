import type {
  FeatureBoardRowDto,
  FeatureBoardSectionDto,
  FeatureBoardViewDto,
} from '../../api/views';

/** One section as drawn: the board's own stories are `direct` ("Directly under …"), a Feature with no rows is `empty`. */
export interface FeatureBoardSectionLayout {
  section: FeatureBoardSectionDto;
  rows: FeatureBoardRowDto[];
  direct: boolean;
  empty: boolean;
}

/**
 * The sections to draw: none for a leaf Feature (one plain grid), else one per Feature in the tree. The board's own section is
 * left out when it has no stories of its own. Rows are grouped by the section they carry.
 */
export function featureBoardLayout(view: FeatureBoardViewDto): FeatureBoardSectionLayout[] {
  if (view.sections.length < 2) return [];
  return view.sections
    .map((section, index) => {
      const rows = view.rows.filter((row) => row.featureId === section.id);
      return { section, rows, direct: index === 0, empty: rows.length === 0 };
    })
    .filter((layout) => !(layout.direct && layout.empty));
}

/** The DOM id a section (and its jump chip's target) has. */
export const sectionDomId = (sectionId: string): string => `feature-board-section-${sectionId}`;

/** Story id → its Feature section, for the cards of a tree with more than one Feature; empty for a leaf. */
export function featureOfStory(
  view: FeatureBoardViewDto,
): ReadonlyMap<string, FeatureBoardSectionDto> {
  const result = new Map<string, FeatureBoardSectionDto>();
  if (view.sections.length < 2) return result;
  const sections = new Map(view.sections.map((s) => [s.id, s]));
  for (const row of view.rows) {
    const section = sections.get(row.featureId);
    if (section) result.set(row.parent.id, section);
  }
  return result;
}
