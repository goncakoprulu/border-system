"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, ArrowRight, LoaderCircle, LockKeyhole } from "lucide-react";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { authApi } from "@/lib/api";
import { siteConfig } from "@/lib/site-config";

const schema = z.object({
  email: z.email("Geçerli bir e-posta adresi girin."),
  password: z.string().min(1, "Parolanızı girin."),
  rememberMe: z.boolean(),
});
type FormValues = z.infer<typeof schema>;

export default function LoginPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: "", password: "", rememberMe: false },
  });
  const login = useMutation({
    mutationFn: authApi.login,
    onSuccess: (user) => {
      queryClient.setQueryData(["current-user"], user);
      router.replace("/dashboard");
    },
  });

  return (
    <main className="grid min-h-screen bg-white lg:grid-cols-[minmax(0,1fr)_minmax(480px,0.72fr)]">
      <section className="relative hidden overflow-hidden bg-[#171916] p-12 text-white lg:flex lg:flex-col lg:justify-between">
        <div className="absolute inset-0 opacity-40 [background-image:radial-gradient(circle_at_20%_20%,#8da26c_0,transparent_32%),radial-gradient(circle_at_80%_80%,#41483a_0,transparent_30%)]" />
        <div className="relative flex items-center gap-3">
          <div className="grid size-11 place-items-center rounded-xl border border-white/20 bg-white/10 font-black tracking-tighter">
            B
          </div>
          <div>
            <p className="text-lg font-semibold tracking-[0.2em]">BORDER</p>
            <p className="text-xs text-white/55">Studio Management</p>
          </div>
        </div>
        <div className="relative max-w-xl">
          <p className="mb-5 text-sm font-medium uppercase tracking-[0.2em] text-[#becba8]">
            Dansın ritmi, tek merkezde
          </p>
          <h1 className="text-5xl font-semibold leading-[1.08] tracking-tight">
            Stüdyonuzu güvenle ve kolayca yönetin.
          </h1>
          <p className="mt-6 max-w-lg text-base leading-7 text-white/60">
            Dersler, öğrenciler ve operasyonlar için sade, güvenli ve mobil
            uyumlu yönetim alanı.
          </p>
        </div>
        <p className="relative text-xs text-white/35">© 2026 BORDER</p>
      </section>

      <section className="flex min-h-screen items-center justify-center px-6 py-12 sm:px-12">
        <div className="w-full max-w-sm">
          <div className="mb-10 flex items-center gap-3 lg:hidden">
            <div className="grid size-10 place-items-center rounded-xl bg-[#1d211b] font-black text-white">
              B
            </div>
            <span className="font-semibold tracking-[0.18em]">BORDER</span>
          </div>
          <div className="mb-8">
            <div className="mb-5 grid size-11 place-items-center rounded-xl bg-[#eef1e9] text-[#4f6240]">
              <LockKeyhole size={20} />
            </div>
            <h2 className="text-3xl font-semibold tracking-tight text-[#1d211b]">
              BORDER Yönetim Sistemi
            </h2>
            <p className="mt-2 text-sm leading-6 text-zinc-500">
              Yetkili kullanıcı hesabınızla giriş yapın.
            </p>
          </div>
          <form
            className="space-y-5"
            onSubmit={handleSubmit((values) => login.mutate(values))}
            noValidate
          >
            <div className="space-y-2">
              <Label htmlFor="email">E-posta</Label>
              <Input
                id="email"
                type="email"
                autoComplete="email"
                placeholder="ad@border.com"
                className="h-11"
                {...register("email")}
              />
              {errors.email && (
                <p className="text-sm text-red-600">{errors.email.message}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="password">Parola</Label>
              <Input
                id="password"
                type="password"
                autoComplete="current-password"
                className="h-11"
                {...register("password")}
              />
              {errors.password && (
                <p className="text-sm text-red-600">
                  {errors.password.message}
                </p>
              )}
            </div>
            <label className="flex cursor-pointer items-center gap-2.5 text-sm text-zinc-600">
              <input
                type="checkbox"
                className="size-4 rounded border-zinc-300 accent-[#4f6240]"
                {...register("rememberMe")}
              />
              Bu cihazda oturumu açık tut
            </label>
            {login.isError && (
              <div
                role="alert"
                className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700"
              >
                {login.error.message}
              </div>
            )}
            <Button
              type="submit"
              className="h-11 w-full bg-[#1d211b] hover:bg-[#30362c]"
              disabled={login.isPending}
            >
              {login.isPending ? (
                <>
                  <LoaderCircle className="animate-spin" />
                  Giriş yapılıyor
                </>
              ) : (
                <>
                  Giriş yap
                  <ArrowRight />
                </>
              )}
            </Button>
          </form>
          <a
            href={siteConfig.publicSiteUrl}
            className="mt-7 inline-flex min-h-11 items-center gap-2 text-sm font-medium text-zinc-500 transition-colors hover:text-[#4f6240] focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-[#4f6240]"
          >
            <ArrowLeft size={16} aria-hidden="true" />
            border.com.tr&apos;ye dön
          </a>
        </div>
      </section>
    </main>
  );
}
