"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { FormEvent, useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { useCurrentUser } from "@/hooks/use-current-user";
import { classesApi, classKeys, InstructorRecord } from "@/lib/classes";

export function InstructorManagementDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const { data: user } = useCurrentUser(); const canEdit = user?.roles.some((x) => x === "Admin" || x === "Management") ?? false; const queryClient = useQueryClient();
  const records = useQuery({ queryKey: ["classes", "instructor-records"], queryFn: classesApi.instructorRecords, enabled: open });
  const logins = useQuery({ queryKey: ["classes", "instructor-logins"], queryFn: classesApi.instructorLogins, enabled: open && canEdit });
  const [editing, setEditing] = useState<InstructorRecord>(); const [firstName, setFirstName] = useState(""); const [lastName, setLastName] = useState(""); const [phone, setPhone] = useState(""); const [email, setEmail] = useState(""); const [userId, setUserId] = useState("");
  const clear = () => { setEditing(undefined); setFirstName(""); setLastName(""); setPhone(""); setEmail(""); setUserId(""); };
  const select = (x: InstructorRecord) => { setEditing(x); setFirstName(x.firstName); setLastName(x.lastName); setPhone(x.phone ?? ""); setEmail(x.email ?? ""); setUserId(x.userId ?? ""); };
  const save = useMutation({ mutationFn: () => { const input = { firstName: firstName.trim(), lastName: lastName.trim(), phone: phone || null, email: email || null, userId: userId || null }; return editing ? classesApi.updateInstructor(editing.id, input) : classesApi.createInstructor(input); }, onSuccess: async () => { await Promise.all([queryClient.invalidateQueries({ queryKey: classKeys.instructors }), queryClient.invalidateQueries({ queryKey: ["classes", "instructor-records"] }), queryClient.invalidateQueries({ queryKey: ["classes", "instructor-logins"] })]); clear(); toast.success("Eğitmen kaydedildi."); }, onError: (e) => toast.error(e.message) });
  const submit = (e: FormEvent) => { e.preventDefault(); if (!firstName.trim() || !lastName.trim()) return toast.error("Ad ve soyad zorunludur."); save.mutate(); };
  return <Dialog open={open} onOpenChange={onOpenChange}><DialogContent className="max-h-[92vh] overflow-y-auto sm:max-w-2xl"><DialogHeader><DialogTitle>Eğitmen yönetimi</DialogTitle><DialogDescription>Eğitmen kartlarını oluşturun; Instructor rolündeki giriş hesabını güvenli sınıf kapsamı için isteğe bağlı bağlayın.</DialogDescription></DialogHeader>
    <div className="max-h-48 divide-y overflow-y-auto rounded-lg border">{records.isLoading ? <p className="p-4 text-sm text-zinc-500">Yükleniyor…</p> : records.data?.length ? records.data.map((x) => <button type="button" key={x.id} onClick={() => canEdit && select(x)} className="flex w-full justify-between p-3 text-left hover:bg-zinc-50"><span><span className="block text-sm font-medium">{x.firstName} {x.lastName}</span><span className="text-xs text-zinc-500">{x.email ?? "E-posta yok"}</span></span><span className="text-xs text-[#526743]">{x.userId ? "Hesap bağlı" : "Hesap bağlı değil"}</span></button>) : <p className="p-4 text-sm text-zinc-500">Henüz eğitmen yok.</p>}</div>
    {canEdit && <form id="instructor-form" onSubmit={submit} className="grid gap-3 sm:grid-cols-2"><Input aria-label="Ad" placeholder="Ad *" value={firstName} onChange={(e) => setFirstName(e.target.value)} /><Input aria-label="Soyad" placeholder="Soyad *" value={lastName} onChange={(e) => setLastName(e.target.value)} /><Input aria-label="Telefon" placeholder="Telefon" value={phone} onChange={(e) => setPhone(e.target.value)} /><Input aria-label="E-posta" placeholder="E-posta" type="email" value={email} onChange={(e) => setEmail(e.target.value)} /><select aria-label="Instructor giriş hesabı" className="h-9 rounded-lg border bg-white px-3 text-sm sm:col-span-2" value={userId} onChange={(e) => setUserId(e.target.value)}><option value="">Giriş hesabı bağlama</option>{logins.data?.filter((x) => !x.linkedInstructorId || x.linkedInstructorId === editing?.id).map((x) => <option key={x.userId} value={x.userId}>{x.displayName} · {x.email}</option>)}</select></form>}
    <DialogFooter><Button variant="outline" onClick={() => onOpenChange(false)}>Kapat</Button>{canEdit && <>{editing && <Button variant="ghost" onClick={clear}>Yeni eğitmen</Button>}<Button form="instructor-form" disabled={save.isPending}>{editing ? "Güncelle" : "Eğitmen ekle"}</Button></>}</DialogFooter>
  </DialogContent></Dialog>;
}
