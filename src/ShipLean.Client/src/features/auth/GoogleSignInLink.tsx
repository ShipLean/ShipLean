import { authTestIds } from './testIds';

/**
 * A full-page link, not a request: the browser has to leave for Google. The mark keeps Google's own colours, which
 * its branding rules require, so these are the one place on this page that doesn't use tokens.
 */
export function GoogleSignInLink({ href }: { href: string }) {
  return (
    <a
      href={href}
      data-testid={authTestIds.googleSignIn}
      className="flex items-center justify-center gap-2 h-8 rounded-sm border border-edge bg-panel px-3 text-[length:var(--text-sm)] font-medium text-ink no-underline hover:bg-canvas"
    >
      <svg aria-hidden="true" viewBox="0 0 18 18" className="size-4 shrink-0">
        <path
          fill="#4285F4"
          d="M17.64 9.2c0-.64-.06-1.25-.16-1.84H9v3.48h4.84a4.14 4.14 0 0 1-1.8 2.72v2.26h2.92c1.7-1.57 2.68-3.88 2.68-6.62z"
        />
        <path
          fill="#34A853"
          d="M9 18c2.43 0 4.47-.8 5.96-2.18l-2.92-2.26c-.8.54-1.83.86-3.04.86-2.34 0-4.32-1.58-5.03-3.7H.96v2.33A9 9 0 0 0 9 18z"
        />
        <path
          fill="#FBBC05"
          d="M3.97 10.72a5.41 5.41 0 0 1 0-3.44V4.95H.96a9 9 0 0 0 0 8.1l3.01-2.33z"
        />
        <path
          fill="#EA4335"
          d="M9 3.58c1.32 0 2.51.45 3.44 1.35l2.58-2.59A8.65 8.65 0 0 0 9 0 9 9 0 0 0 .96 4.95l3.01 2.33C4.68 5.16 6.66 3.58 9 3.58z"
        />
      </svg>
      Sign in with Google
    </a>
  );
}
