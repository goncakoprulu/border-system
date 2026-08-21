"use client";

import { useQuery } from "@tanstack/react-query";
import { authApi } from "@/lib/api";

export function useCurrentUser() {
  return useQuery({ queryKey: ["current-user"], queryFn: authApi.me });
}
