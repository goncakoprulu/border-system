"use client";

import { useQuery } from "@tanstack/react-query";
import { GraduationCap, Search, UserCog, Users } from "lucide-react";
import Link from "next/link";
import { useDeferredValue, useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { formErrorMessage } from "@/lib/form-errors";
import { operationsApi } from "@/lib/operations";

const icons = { Student: Users, Class: GraduationCap, Instructor: UserCog };
const labels = { Student: "Öğrenci", Class: "Sınıf", Instructor: "Eğitmen" };

export function GlobalSearch() {
  const [open, setOpen] = useState(false);
  const [value, setValue] = useState("");
  const query = useDeferredValue(value.trim());
  const results = useQuery({ queryKey: ["global-search", query], queryFn: () => operationsApi.search(query), enabled: open && query.length >= 2 });

  useEffect(() => {
    const handler = (event: KeyboardEvent) => { if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") { event.preventDefault(); setOpen(true); } };
    window.addEventListener("keydown", handler); return () => window.removeEventListener("keydown", handler);
  }, []);

  return <>
    <Button type="button" variant="outline" className="h-9 min-w-9 justify-start px-2.5 sm:w-64" onClick={() => setOpen(true)} aria-label="Hızlı arama"><Search /><span className="hidden flex-1 text-left text-zinc-500 sm:inline">Öğrenci, sınıf, eğitmen ara</span><kbd className="hidden rounded border bg-zinc-50 px-1.5 py-0.5 text-[10px] text-zinc-400 sm:inline">Ctrl K</kbd></Button>
    <Dialog open={open} onOpenChange={(next) => { setOpen(next); if (!next) setValue(""); }}><DialogContent className="top-[12%] max-h-[78vh] translate-y-0 overflow-hidden p-0 sm:max-w-xl"><DialogHeader className="px-5 pt-5"><DialogTitle>Hızlı arama</DialogTitle><DialogDescription>Öğrenci adı, telefon, e-posta; sınıf veya eğitmen bilgisi yazın.</DialogDescription></DialogHeader>
      <div className="relative px-5"><Search className="absolute left-8 top-1/2 -translate-y-1/2 text-zinc-400" size={17}/><Input autoFocus value={value} onChange={(event) => setValue(event.target.value)} className="h-11 pl-10" placeholder="En az 2 karakter yazın..." /></div>
      <div className="min-h-40 overflow-y-auto border-t">
        {query.length < 2 ? <Message text="Aramaya başlamak için en az iki karakter yazın." /> : results.isLoading ? <div className="space-y-2 p-4">{Array.from({length:4}).map((_,index)=><div key={index} className="h-14 animate-pulse rounded-lg bg-zinc-100"/>)}</div> : results.isError ? <div className="grid min-h-40 place-items-center gap-3 p-6 text-center text-sm text-red-700"><p>{formErrorMessage(results.error,"Arama yapılırken beklenmeyen bir hata oluştu.")}</p><Button type="button" size="sm" variant="outline" onClick={() => results.refetch()}>Tekrar dene</Button></div> : results.data?.items.length ? <div className="divide-y">{results.data.items.map((item) => { const Icon=icons[item.type]; return <Link key={`${item.type}-${item.id}`} href={item.href} onClick={() => setOpen(false)} className="flex min-h-16 items-center gap-3 px-5 py-3 hover:bg-zinc-50"><div className="grid size-9 shrink-0 place-items-center rounded-lg bg-[#edf1e9] text-[#526743]"><Icon size={17}/></div><div className="min-w-0 flex-1"><p className="truncate font-medium">{item.label}</p><p className="truncate text-xs text-zinc-500">{labels[item.type]}{item.detail?` · ${item.detail}`:""}</p></div></Link>; })}</div> : <Message text="Eşleşen kayıt bulunamadı." />}
      </div>
    </DialogContent></Dialog>
  </>;
}

function Message({ text }: { text:string }) { return <div className="grid min-h-40 place-items-center p-6 text-center text-sm text-zinc-500">{text}</div>; }
