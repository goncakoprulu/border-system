"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { LoaderCircle, Plus } from "lucide-react";
import { FormEvent, useRef, useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { ApiError } from "@/lib/api";
import { classesApi, classKeys, StudioRoom } from "@/lib/classes";
import { formErrorMessage } from "@/lib/form-errors";

export function RoomManagementDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const queryClient = useQueryClient(); const rooms = useQuery({ queryKey: classKeys.rooms, queryFn: classesApi.rooms, enabled: open });
  const saveLock = useRef(false);
  const [editing, setEditing] = useState<StudioRoom>(); const [name, setName] = useState(""); const [capacity, setCapacity] = useState("");
  const [errors, setErrors] = useState<{ name?: string; capacity?: string; form?: string }>({});
  const clear = () => { setEditing(undefined); setName(""); setCapacity(""); setErrors({}); };
  const save = useMutation({
    mutationFn: () => { const input = { name: name.trim(), description: null, capacity: capacity ? Number(capacity) : null, isActive: editing?.isActive ?? true }; return editing ? classesApi.updateRoom(editing.id, input) : classesApi.createRoom(input); },
    onSuccess: async (room) => { queryClient.setQueryData<StudioRoom[]>(classKeys.rooms, (current) => current?.some((item) => item.id === room.id) ? current.map((item) => item.id === room.id ? room : item) : [...(current ?? []), room]); await queryClient.invalidateQueries({ queryKey: classKeys.rooms }); clear(); toast.success(editing ? "Stüdyo başarıyla güncellendi." : "Stüdyo başarıyla oluşturuldu."); },
    onError: (error) => { const fieldErrors = error instanceof ApiError ? error.errors : undefined; const next = { name: fieldErrors?.Name?.[0] ?? fieldErrors?.name?.[0], capacity: fieldErrors?.Capacity?.[0] ?? fieldErrors?.capacity?.[0], form: formErrorMessage(error, "Stüdyo kaydedilirken beklenmeyen bir hata oluştu.") }; setErrors(next); toast.error(next.form); },
    onSettled: () => { saveLock.current = false; },
  });
  const archive = useMutation({
    mutationFn: (id: string) => classesApi.archiveRoom(id),
    onSuccess: async () => { await queryClient.invalidateQueries({ queryKey: classKeys.rooms }); clear(); toast.success("Stüdyo arşivlendi."); },
    onError: (error) => { const message = formErrorMessage(error, "Stüdyo arşivlenirken beklenmeyen bir hata oluştu."); setErrors((current) => ({ ...current, form: message })); toast.error(message); },
  });
  const submit = (e: FormEvent) => { e.preventDefault(); if (saveLock.current || save.isPending) return; const next: typeof errors = {}; if (!name.trim()) next.name = "Stüdyo adı zorunludur."; if (capacity && (!Number.isInteger(Number(capacity)) || Number(capacity) <= 0 || Number(capacity) > 1000)) next.capacity = "Kapasite 1 ile 1000 arasında olmalıdır."; setErrors(next); if (next.name || next.capacity) return; saveLock.current = true; save.mutate(); };
  return <Dialog open={open} onOpenChange={onOpenChange}><DialogContent className="sm:max-w-xl"><DialogHeader><DialogTitle>Stüdyo yönetimi</DialogTitle><DialogDescription>Sınıf programlarında kullanılacak aktif salonları yönetin.</DialogDescription></DialogHeader>
    <div className="max-h-56 divide-y overflow-y-auto rounded-lg border">{rooms.isLoading ? <p className="p-4 text-sm text-zinc-500">Yükleniyor…</p> : rooms.isError ? <p role="alert" className="p-4 text-sm text-red-600">{rooms.error.message}</p> : rooms.data?.length ? rooms.data.map((room) => <button type="button" key={room.id} disabled={save.isPending} onClick={() => { setEditing(room); setName(room.name); setCapacity(room.capacity?.toString() ?? ""); setErrors({}); }} className="flex w-full items-center justify-between p-3 text-left hover:bg-zinc-50"><span><span className="block text-sm font-medium">{room.name}</span><span className="text-xs text-zinc-500">{room.capacity ? `${room.capacity} kişi` : "Kapasite belirtilmedi"}</span></span><span className="text-xs text-[#526743]">Düzenle</span></button>) : <p className="p-4 text-sm text-zinc-500">Henüz stüdyo yok.</p>}</div>
    {errors.form && <p role="alert" className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700">{errors.form}</p>}
    <form id="room-form" className="grid gap-3 sm:grid-cols-[1fr_130px]" onSubmit={submit} noValidate><div><Input aria-label="Stüdyo adı" aria-invalid={!!errors.name} placeholder="Stüdyo adı" value={name} disabled={save.isPending} onChange={(e) => { setName(e.target.value); setErrors((current) => ({ ...current, name: undefined, form: undefined })); }} />{errors.name && <p className="mt-1 text-xs text-red-600">{errors.name}</p>}</div><div><Input aria-label="Kapasite" aria-invalid={!!errors.capacity} placeholder="Kapasite" type="number" min={1} max={1000} value={capacity} disabled={save.isPending} onChange={(e) => { setCapacity(e.target.value); setErrors((current) => ({ ...current, capacity: undefined, form: undefined })); }} />{errors.capacity && <p className="mt-1 text-xs text-red-600">{errors.capacity}</p>}</div></form>
    <DialogFooter>{editing && <Button type="button" variant="outline" className="mr-auto text-rose-700" disabled={save.isPending || archive.isPending} onClick={() => { if (window.confirm(`${editing.name} stüdyosunu arşivlemek istediğinize emin misiniz?`)) archive.mutate(editing.id); }}>{archive.isPending && <LoaderCircle className="animate-spin" />}Arşivle</Button>}<Button type="button" variant="outline" disabled={save.isPending || archive.isPending} onClick={() => onOpenChange(false)}>Kapat</Button>{editing && <Button type="button" variant="ghost" disabled={save.isPending || archive.isPending} onClick={clear}><Plus />Yeni</Button>}<Button type="submit" form="room-form" disabled={save.isPending || archive.isPending} aria-busy={save.isPending}>{save.isPending && <LoaderCircle className="animate-spin" />}{save.isPending ? "Kaydediliyor..." : editing ? "Güncelle" : "Stüdyo ekle"}</Button></DialogFooter>
  </DialogContent></Dialog>;
}
