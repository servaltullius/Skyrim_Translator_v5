import * as React from "react";
export function useIsMobile(breakpoint = 768) {
  const get = () =>
    typeof window !== "undefined" &&
    typeof window.matchMedia === "function" &&
    window.matchMedia(`(max-width: ${breakpoint}px)`).matches;
  const [isMobile, setIsMobile] = React.useState<boolean>(get());
  React.useEffect(() => {
    if (typeof window === "undefined" || typeof window.matchMedia !== "function") return;
    const mql = window.matchMedia(`(max-width: ${breakpoint}px)`);
    const handler = () => setIsMobile(mql.matches);
    mql.addEventListener?.("change", handler);
    // Safari fallback
    // @ts-ignore
    mql.addListener?.(handler);
    handler();
    return () => {
      mql.removeEventListener?.("change", handler);
      // @ts-ignore
      mql.removeListener?.(handler);
    };
  }, [breakpoint]);
  return isMobile;
}
