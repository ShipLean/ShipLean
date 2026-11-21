import type { PromptDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../testIds';

// "The built-in prompt changed": Review the change, and for admins Apply to my version, Apply and edit, Keep mine.
export function UpdateBanner(props: {
  projectId: string;
  prompt: PromptDto;
  canEdit: boolean;
  onApplyAndEdit: () => void;
  onReview: () => void;
}) {
  // TODO: the banner text and its actions
  return <div data-testid={ids.updateBanner} data-can-edit={props.canEdit} hidden />;
}
