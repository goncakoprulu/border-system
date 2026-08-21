"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { LoaderCircle, Plus, Trash2 } from "lucide-react";
import { useRouter } from "next/navigation";
import { useEffect } from "react";
import { useFieldArray, useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { ApiError } from "@/lib/api";
import { ClassDetail, ClassInput, classesApi, classKeys, classStatuses, classStatusLabels, dayLabels } from "@/lib/classes";
import { classDetailHref } from "@/lib/routes";

const scheduleSchema = z.object({ dayOfWeek: z.number().min(0).max(6), startTime: z.string().min(1), endTime: z.string().min(1) }).refine((value) => value.endTime > value.startTime, { path: ["endTime"], message: "Bitiş saati başlangıçtan sonra olmalı." });
const schema = z.object({
  name: z.string().trim().min(1, "Sınıf adı zorunludur.").max(160), description: z.string().max(2000), instructorId: z.string().min(1, "Eğitmen seçin."), studioRoomId: z.string().min(1, "Stüdyo seçin."),
  capacity: z.number().int().min(1).max(500), level: z.string().max(80), ageGroup: z.string().max(80), status: z.enum(classStatuses), startDate: z.string().min(1, "Başlangıç tarihi zorunludur."), endDate: z.string(), schedules: z.array(scheduleSchema).max(14),
}).refine((value) => !value.endDate || value.endDate >= value.startDate, { path: ["endDate"], message: "Bitiş tarihi başlangıçtan önce olamaz." });
type FormValues = z.infer<typeof schema>;
const today = () => new Intl.DateTimeFormat("en-CA", { timeZone: "Europe/Istanbul" }).format(new Date());
const defaults = (item?: ClassDetail): FormValues => ({ name: item?.name ?? "", description: item?.description ?? "", instructorId: item?.instructorId ?? "", studioRoomId: item?.studioRoomId ?? "", capacity: item?.capacity ?? 12, level: item?.level ?? "", ageGroup: item?.ageGroup ?? "", status: item?.status ?? "Planned", startDate: item?.startDate ?? today(), endDate: item?.endDate ?? "", schedules: item?.schedules.map((x) => ({ dayOfWeek: x.dayOfWeek, startTime: x.startTime.slice(0, 5), endTime: x.endTime.slice(0, 5) })) ?? [] });

export function ClassFormDialog({ open, onOpenChange, item }: { open: boolean; onOpenChange: (open: boolean) => void; item?: ClassDetail }) {
  const router = useRouter(); const queryClient = useQueryClient();
  const instructors = useQuery({ queryKey: classKeys.instructors, queryFn: classesApi.instructors, enabled: open });
  const rooms = useQuery({ queryKey: classKeys.rooms, queryFn: classesApi.rooms, enabled: open });
  const { register, control, reset, handleSubmit, setError, formState: { errors } } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: defaults(item) });
  const schedules = useFieldArray({ control, name: "schedules" });
  useEffect(() => { if (open) reset(defaults(item)); }, [item, open, reset]);
  const mutation = useMutation({ mutationFn: (values: FormValues) => { const input: ClassInput = { ...values, description: values.description || null, level: values.level || null, ageGroup: values.ageGroup || null, endDate: values.endDate || null }; return item ? classesApi.update(item.id, input) : classesApi.create(input); }, onSuccess: async (result) => { await queryClient.invalidateQueries({ queryKey: classKeys.all }); onOpenChange(false); toast.success(item ? "Sınıf güncellendi." : "Sınıf oluşturuldu."); router.push(classDetailHref(result.id)); }, onError: (error) => { if (error instanceof ApiError && error.errors) Object.entries(error.errors).forEach(([key, messages]) => setError(key[0].toLowerCase() + key.slice(1) as keyof FormValues, { message: messages[0] })); else toast.error(error.message); } });
  const field = (id: keyof FormValues, label: string, node: React.ReactNode) => <div className="space-y-2"><Label htmlFor={id}>{label}</Label>{node}{errors[id] && <p className="text-xs text-red-600">{errors[id]?.message as string}</p>}</div>;
  return <Dialog open={open} onOpenChange={onOpenChange}><DialogContent className="max-h-[94vh] overflow-y-auto sm:max-w-3xl"><DialogHeader><DialogTitle>{item ? "Sınıfı düzenle" : "Yeni sınıf"}</DialogTitle><DialogDescription>Sınıf bilgilerini ve tekrar eden haftalık programı birlikte tanımlayın.</DialogDescription></DialogHeader>
    <form id="class-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="grid gap-5 sm:grid-cols-2" noValidate>
      {field("name", "Sınıf adı *", <Input id="name" autoFocus {...register("name")} />)}
      {field("status", "Durum", <select id="status" className="h-9 w-full rounded-lg border bg-white px-3 text-sm" {...register("status")}>{classStatuses.map((x) => <option key={x} value={x}>{classStatusLabels[x]}</option>)}</select>)}
      {field("instructorId", "Eğitmen *", <select id="instructorId" className="h-9 w-full rounded-lg border bg-white px-3 text-sm" {...register("instructorId")}><option value="">Seçin</option>{instructors.data?.map((x) => <option key={x.id} value={x.id}>{x.fullName}</option>)}</select>)}
      {field("studioRoomId", "Stüdyo *", <select id="studioRoomId" className="h-9 w-full rounded-lg border bg-white px-3 text-sm" {...register("studioRoomId")}><option value="">Seçin</option>{rooms.data?.filter((x) => x.isActive && !x.isArchived).map((x) => <option key={x.id} value={x.id}>{x.name}{x.capacity ? ` · ${x.capacity} kişi` : ""}</option>)}</select>)}
      {field("capacity", "Kapasite *", <Input id="capacity" type="number" min={1} max={500} {...register("capacity", { valueAsNumber: true })} />)}
      {field("level", "Seviye", <Input id="level" {...register("level")} />)}
      {field("ageGroup", "Yaş grubu", <Input id="ageGroup" placeholder="Örn. 8–12" {...register("ageGroup")} />)}
      <div />
      {field("startDate", "Başlangıç tarihi *", <Input id="startDate" type="date" {...register("startDate")} />)}
      {field("endDate", "Bitiş tarihi", <Input id="endDate" type="date" {...register("endDate")} />)}
      <div className="space-y-2 sm:col-span-2"><Label htmlFor="description">Açıklama</Label><Textarea id="description" rows={3} {...register("description")} /></div>
      <div className="sm:col-span-2"><div className="mb-3 flex items-center justify-between"><div><Label>Haftalık program</Label><p className="mt-1 text-xs text-zinc-500">Çakışan eğitmen ve stüdyo saatleri kaydedilmez.</p></div><Button type="button" size="sm" variant="outline" onClick={() => schedules.append({ dayOfWeek: 1, startTime: "18:00", endTime: "19:00" })}><Plus />Saat ekle</Button></div>
        <div className="space-y-2">{schedules.fields.length === 0 && <p className="rounded-lg border border-dashed p-4 text-center text-sm text-zinc-500">Program satırı eklenmedi.</p>}{schedules.fields.map((row, index) => <div key={row.id} className="grid grid-cols-[1fr_96px_96px_36px] gap-2"><select aria-label={`${index + 1}. gün`} className="h-9 min-w-0 rounded-lg border bg-white px-2 text-sm" {...register(`schedules.${index}.dayOfWeek`, { valueAsNumber: true })}>{dayLabels.map((label, day) => <option key={label} value={day}>{label}</option>)}</select><Input aria-label="Başlangıç saati" type="time" {...register(`schedules.${index}.startTime`)} /><Input aria-label="Bitiş saati" type="time" {...register(`schedules.${index}.endTime`)} /><Button type="button" variant="ghost" size="icon-sm" aria-label="Satırı sil" onClick={() => schedules.remove(index)}><Trash2 /></Button>{errors.schedules?.[index]?.endTime && <p className="col-span-4 text-xs text-red-600">{errors.schedules[index].endTime.message}</p>}</div>)}</div>
      </div>
    </form><DialogFooter><Button variant="outline" onClick={() => onOpenChange(false)}>Vazgeç</Button><Button form="class-form" disabled={mutation.isPending}>{mutation.isPending && <LoaderCircle className="animate-spin" />}{item ? "Değişiklikleri kaydet" : "Sınıfı oluştur"}</Button></DialogFooter>
  </DialogContent></Dialog>;
}
