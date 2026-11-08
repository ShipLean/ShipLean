// A minimal component for the CT phone project's own check (PhoneProject.phone.spec.tsx): something to mount, nothing
// else. It has no test id; the spec asserts on the locator mount returns.
export function PhoneProbe() {
  return (
    <div className="p-4 text-sm">
      <p>Phone probe</p>
    </div>
  );
}
