import { useState } from 'react';
import type { ProjectDemoBrandDto } from '../../../../api/demos';
import { showToast } from '../../../../components/toastStore';
import { useRemoveDemoLogo, useUploadDemoLogo } from '../../../demos/demosApi';
import { demosTestIds as ids } from '../../../demos/testIds';

const previewBox =
  'grid h-[72px] w-[200px] place-items-center overflow-hidden rounded-control border border-edge bg-sunken p-2';

/** The logo: previewed through an img, uploaded and removed with the server's message shown beside it. */
export function LogoField({
  projectId,
  brand,
  readOnly,
}: {
  projectId: string;
  brand: ProjectDemoBrandDto;
  readOnly: boolean;
}) {
  const upload = useUploadDemoLogo(projectId);
  const remove = useRemoveDemoLogo(projectId);
  const [error, setError] = useState<string | null>(null);
  const busy = upload.isPending || remove.isPending;

  const fail = (failure: Error) => {
    setError(failure.message);
    showToast(failure.message, { tone: 'danger' });
  };
  const done = () => setError(null);

  return (
    <div className="grid gap-2 text-[length:var(--text-sm)]">
      <span className="font-medium text-ink">Logo</span>
      <div className="flex flex-wrap items-center gap-4">
        {brand.logoUrl === null ? (
          <span data-testid={ids.demoLogoPreview} className={previewBox}>
            <svg
              viewBox="0 0 120 32"
              role="img"
              aria-label="ShipLean logo"
              className="h-10 w-[150px]"
            >
              <rect x="1" y="4" width="24" height="24" rx="6" fill="var(--color-accent)" />
              <text
                x="34"
                y="23"
                fontSize="17"
                fontWeight="700"
                fill="currentColor"
                fontFamily="var(--font-sans)"
              >
                ShipLean
              </text>
            </svg>
          </span>
        ) : (
          <span className={previewBox}>
            <img
              data-testid={ids.demoLogoPreview}
              src={brand.logoUrl}
              alt="This project's logo"
              className="max-h-14 max-w-44 object-contain"
            />
          </span>
        )}
        <div className="grid justify-items-start gap-1">
          {brand.logoUrl === null ? (
            <span className="flex items-center gap-2">
              <b className="text-ink">ShipLean&apos;s logo</b>
              <span
                data-testid={ids.demoLogoDefault}
                className="rounded-full bg-signal-soft px-[7px] text-[length:var(--text-xs)] font-medium text-ink"
              >
                Default
              </span>
            </span>
          ) : (
            <b className="text-ink">This project&apos;s logo</b>
          )}
          {brand.logoUrl === null ? (
            <span className="text-[length:var(--text-xs)] text-ink-muted">
              Used because this project hasn&apos;t uploaded its own.
            </span>
          ) : null}
          {readOnly ? null : (
            <span className="flex items-center gap-2">
              <label className="btn-ghost cursor-pointer">
                {brand.logoUrl === null ? 'Upload a logo' : 'Replace'}
                <input
                  type="file"
                  hidden
                  data-testid={ids.demoLogoFile}
                  accept="image/png,image/jpeg,image/webp,image/svg+xml"
                  disabled={busy}
                  onChange={(event) => {
                    const file = event.target.files?.[0];
                    event.target.value = '';
                    if (file !== undefined) upload.mutate(file, { onSuccess: done, onError: fail });
                  }}
                />
              </label>
              {brand.logoUrl === null ? null : (
                <button
                  type="button"
                  data-testid={ids.demoLogoRemove}
                  className="btn-ghost text-alert"
                  disabled={busy}
                  onClick={() => remove.mutate(undefined, { onSuccess: done, onError: fail })}
                >
                  Remove
                </button>
              )}
            </span>
          )}
        </div>
      </div>
      {error === null ? null : (
        <div
          data-testid={ids.demoLogoError}
          role="alert"
          className="max-w-xl border-l-[3px] border-alert px-2.5 py-1.5 text-alert"
        >
          {error}
        </div>
      )}
      <span className="text-[length:var(--text-xs)] text-ink-muted">
        PNG, JPEG, WebP or SVG, up to 1 MiB. An SVG is cleaned: scripts and outside links are
        removed.
      </span>
    </div>
  );
}
