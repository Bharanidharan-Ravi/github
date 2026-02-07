import React,{ useEffect, useState } from 'react';

export function useResponsive(breakpoint = 768) {
  const [isMobile, setIsMobile] = useState(false);

  useEffect(() => {
    if (typeof window === 'undefined') return;

    const handler = () =>
      setIsMobile(window.innerWidth < breakpoint);

    handler();
    window.addEventListener('resize', handler);
    return () => window.removeEventListener('resize', handler);
  }, [breakpoint]);

  return isMobile;
}
