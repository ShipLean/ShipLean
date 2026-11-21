import type { RetroCategory } from '@shiplean/core/api/retros';
import { retroCategoryColours, retroCategoryLabels } from './retroCategories';

// Four clearly different shapes, drawn in the category's colour: a tick in a circle (went well), a warning triangle (went badly),
// a rising arrow (can be improved) and a dashed square with a plus (not in the plan).
const paths: Record<RetroCategory, React.ReactNode> = {
  wentWell: (
    <>
      <circle cx="8" cy="8" r="6.25" />
      <path d="M5.2 8.2l2 2 3.6-4.2" />
    </>
  ),
  wentBadly: (
    <>
      <path d="M8 1.9L14.6 13.5H1.4Z" />
      <path d="M8 6.4v3.2M8 11.5v.1" />
    </>
  ),
  improve: (
    <>
      <path d="M1.5 12l4.2-4.4 2.6 2.6L14 4.2" />
      <path d="M10 4h4.2v4.2" />
    </>
  ),
  notInPlan: (
    <>
      <rect x="2" y="2" width="12" height="12" rx="2" strokeDasharray="2.4 2" />
      <path d="M8 5.2v5.6M5.2 8h5.6" />
    </>
  ),
};

/** The category's icon in its colour. Named by default (role img, label and hover title); `decorative` hides it where the name is written next to it. */
export function RetroCategoryIcon({
  category,
  decorative = false,
}: {
  category: RetroCategory;
  decorative?: boolean;
}) {
  const label = retroCategoryLabels[category];
  return (
    <svg
      width="16"
      height="16"
      viewBox="0 0 16 16"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.6"
      strokeLinecap="round"
      strokeLinejoin="round"
      className="flex-none"
      style={{ color: retroCategoryColours[category] }}
      data-category={category}
      {...(decorative ? { 'aria-hidden': true } : { role: 'img', 'aria-label': label })}
    >
      {decorative ? null : <title>{label}</title>}
      {paths[category]}
    </svg>
  );
}
