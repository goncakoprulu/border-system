"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus } from "lucide-react";
import { FormEvent, useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { classesApi, classKeys, StudioRoom } from "@/lib/classes";
import { formErrorMessage } from "@/lib/form-errors";

export function RoomManagementDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const queryClient = useQueryClient(); const rooms = useQuery({ queryKey: classKeys.rooms, queryFn: classesApi.rooms, enabled: open });
  const [editing, setEditing] = useState<StudioRoom>(); const [name, setName] = useState(""); const [capacity, setCapacity] = useState("");
  const save = useMutation({ mutationFn: () => { const input = { name: name.trim(), description: null, capacity: capacity ? Number(capacity) : null, isActive: true }; return editing ? classesApi.updateRoom(editing.id, input) : classesApi.createRoom(input); }, onSuccess: async () => { await queryClient.invalidateQueries({ queryKey: classKeys.rooms }); setEditing(undefined); setName(""); setCapacity(""); toast.success("Stüdyo kaydedildi."); }, onError: (error) => toast.error(formErrorMessage(error, "Stüdyo kaydedilirken beklenmeyen bir hata oluştu.")) });
  const submit = (e: FormEvent) => { e.preventDefault(); if (!name.trim()) return toast.error("Stüdyo adı zorunludur."); save.mutate(); };
  return <Dialog open={open} onOpenChange={onOpenChange}><DialogContent className="sm:max-w-xl"><DialogHeader><DialogTitle>Stüdyo yönetimi</DialogTitle><DialogDescription>Sınıf programlarında kullanılacak aktif salonları yönetin.</DialogDescription></DialogHeader>
    <div className="max-h-56 divide-y overflow-y-auto rounded-lg border">{rooms.isLoading ? <p className="p-4 text-sm text-zinc-500">Yükleniyor…</p> : rooms.data?.length ? rooms.data.map((room) => <button type="button" key={room.id} onClick={() => { setEditing(room); setName(room.name); setCapacity(room.capacity?.toString() ?? ""); }} className="flex w-full items-center justify-between p-3 text-left hover:bg-zinc-50"><span><span className="block text-sm font-medium">{room.name}</span><span className="text-xs text-zinc-500">{room.capacity ? `${room.capacity} kişi` : "Kapasite belirtilmedi"}</span></span><span className="text-xs text-[#526743]">Düzenle</span></button>) : <p className="p-4 text-sm text-zinc-500">Henüz stüdyo yok.</p>}</div>
    <form id="room-form" className="grid gap-3 sm:grid-cols-[1fr_130px]" onSubmit={submit}><Input aria-label="Stüdyo adı" placeholder="Stüdyo adı" value={name} onChange={(e) => setName(e.target.value)} /><Input aria-label="Kapasite" placeholder="Kapasite" type="number" min={1} value={capacity} onChange={(e) => setCapacity(e.target.value)} /></form>
    <DialogFooter><Button variant="outline" onClick={() => onOpenChange(false)}>Kapat</Button>{editing && <Button variant="ghost" onClick={() => { setEditing(undefined); setName(""); setCapacity(""); }}><Plus />Yeni</Button>}<Button form="room-form" disabled={save.isPending}>{editing ? "Güncelle" : "Stüdyo ekle"}</Button></DialogFooter>
  </DialogContent></Dialog>;
}
