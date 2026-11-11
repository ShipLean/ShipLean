import type { ReactNode } from 'react';
import { BacklogProvider } from './providers/backlog';
import { FeaturesProvider } from './providers/features';
import { InboxProvider } from './providers/inbox';
import { RetroProvider } from './providers/retro';
import { RunnerProvider } from './providers/runner';
import { VoiceProvider } from './providers/voice';
import { WorkItemProvider } from './providers/workItem';

export interface ProjectProvidersProps {
  projectKey: string;
  children: ReactNode;
}

// The provider slots around one project's tabs and stack screens, outermost first. Each slot's owner replaces only
// its slot file.
export function ProjectProviders({ projectKey, children }: ProjectProvidersProps) {
  return (
    <RunnerProvider projectKey={projectKey}>
      <VoiceProvider projectKey={projectKey}>
        <InboxProvider projectKey={projectKey}>
          <WorkItemProvider projectKey={projectKey}>
            <BacklogProvider projectKey={projectKey}>
              <FeaturesProvider projectKey={projectKey}>
                <RetroProvider projectKey={projectKey}>{children}</RetroProvider>
              </FeaturesProvider>
            </BacklogProvider>
          </WorkItemProvider>
        </InboxProvider>
      </VoiceProvider>
    </RunnerProvider>
  );
}
