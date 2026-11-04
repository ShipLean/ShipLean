import type { DemoProps } from './DemoProps';
import { demosTestIds as ids } from './testIds';

// TODO: the sanitised script, rendered as HTML.
export function DemoScript({ demo }: DemoProps) {
  return (
    <div data-testid={ids.demoScript}>{demo.scriptHtml === null ? 'No script' : 'Script'}</div>
  );
}
