// What the Prompts tab shows at its right end: "N custom" once the page has loaded and any prompt is custom.
export function PromptsTabBadge(props: { projectId: string }) {
  // TODO: read the custom count from the page query and show it
  return props.projectId === '' ? null : null;
}
