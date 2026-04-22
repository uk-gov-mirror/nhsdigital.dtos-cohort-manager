"use client";

import { useCallback } from "react";
import { useIdleTimer } from "react-idle-timer";
import { signOut } from "next-auth/react";

export default function IdleLogout() {
  const onIdle = useCallback(() => {
    signOut({ callbackUrl: "/" });
  }, []);

  useIdleTimer({
    onIdle,
    timeout: 60 * 1000,
    debounce: 500,
  });
  return null;
}
