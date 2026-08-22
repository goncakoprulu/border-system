"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { CircleAlert, LoaderCircle, Plus, Trash2 } from "lucide-react";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { FieldErrors, useFieldArray, useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { applyApiFieldErrors, formErrorMessage } from "@/lib/form-errors";
import { ClassDetail, ClassInput, classesApi, classKeys, classStatuses, classStatusLabels, dayLabels } from "@/lib/classes";
import { classDetailHref } from "@/lib/routes";

const scheduleSchema = z.object({
  dayOfWeek: z.number({ error: "Gün seçin." }).int().min(0, "Gün seçin.").max(6, "Gün seçin."),
  startTime: z.string().min(1, "Başlangıç saati zorunludur."),
  endTime: z.string().min(1, "Bitiş saati zorunludur."),
}).refine((value) => !value.startTime || !value.endTime || value.endTime > value.startTime, {
  path: ["endTime"], message: "Bitiş saati başlangıç saatinden sonra olmalı.",
});

export const classFormSchema = z.object({
  name: z.string().trim().min(1, "Sınıf adı zorunludur.").max(160, "Sınıf adı en fazla 160 karakter olabilir."),
  description: z.string().max(2000, "Açıklama en fazla 2000 karakter olabilir."),
  instructorId: z.string().min(1, "Eğitmen seçin."),
  studioRoomId: z.string().min(1, "Stüdyo seçin."),
  capacity: z.number({ error: "Geçerli bir kapasite girin." }).int("Kapasite tam sayı olmalıdır.").min(1, "Kapasite 0'dan büyük olmalıdır.").max(500, "Kapasite en fazla 500 olabilir."),
  level: z.string().max(80, "Seviye en fazla 80 karakter olabilir."),
  ageGroup: z.string().max(80, "Yaş grubu en fazla 80 karakter olabilir."),
  status: z.enum(classStatuses),
  startDate: z.string().min(1, "Başlangıç tarihi zorunludur."),
  endDate: z.string(),
  schedules: z.array(scheduleSchema).max(14, "En fazla 14 program satırı eklenebilir."),
}).superRefine((value, context) => {
  if (value.endDate && value.startDate && value.endDate < value.startDate) {
    context.addIssue({ code: "custom", path: ["endDate"], message: "Bitiş tarihi başlangıçtan önce olamaz." });
  }
  const seen = new Map<string, number>();
  value.schedules.forEach((row, index) => {
    const key = `${row.dayOfWeek}|${row.startTime}|${row.endTime}`;
    const firstIndex = seen.get(key);
    if (firstIndex === undefined) seen.set(key, index);
    else context.addIssue({ code: "custom", path: ["schedules", index, "dayOfWeek"], message: `${firstIndex + 1}. satırla aynı program tekrar edemez.` });
  });
});

export type ClassFormValues = z.infer<typeof classFormSchema>;
const topLevelFields = ["name", "description", "instructorId", "studioRoomId", "capacity", "level", "ageGroup", "status", "startDate", "endDate", "schedules"] as const;
const today = () => new Intl.DateTimeFormat("en-CA", { timeZone: "Europe/Istanbul" }).format(new Date());
const defaults = (item?: ClassDetail): ClassFormValues => ({
  name: item?.name ?? "", description: item?.description ?? "", instructorId: item?.instructorId ?? "", studioRoomId: item?.studioRoomId ?? "",
  capacity: item?.capacity ?? 12, level: item?.level ?? "", ageGroup: item?.ageGroup ?? "", status: item?.status ?? "Planned",
  startDate: item?.startDate ?? today(), endDate: item?.endDate ?? "",
  schedules: item?.schedules.map((schedule) => ({ dayOfWeek: schedule.dayOfWeek, startTime: schedule.startTime.slice(0, 5), endTime: schedule.endTime.slice(0, 5) })) ?? [],
});

export function ClassFormDialog({ open, onOpenChange, item }: { open: boolean; onOpenChange: (open: boolean) => void; item?: ClassDetail }) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const [submitError, setSubmitError] = useState<string>();
  const instructors = useQuery({ queryKey: classKeys.instructors, queryFn: classesApi.instructors, enabled: open });
  const rooms = useQuery({ queryKey: classKeys.rooms, queryFn: classesApi.rooms, enabled: open });
  const { register, control, reset, handleSubmit, setError, formState: { errors } } = useForm<ClassFormValues>({ resolver: zodResolver(classFormSchema), defaultValues: defaults(item), shouldFocusError: true });
  const schedules = useFieldArray({ control, name: "schedules" });

  useEffect(() => {
    if (!open) return;
    reset(defaults(item));
  }, [item, open, reset]);

  const mutation = useMutation({
    mutationFn: (values: ClassFormValues) => {
      const input: ClassInput = { ...values, description: values.description || null, level: values.level || null, ageGroup: values.ageGroup || null, endDate: values.endDate || null };
      return item ? classesApi.update(item.id, input) : classesApi.create(input);
    },
    onSuccess: async (result) => {
      queryClient.setQueryData(classKeys.detail(result.id), result);
      await queryClient.invalidateQueries({ queryKey: classKeys.all });
      setSubmitError(undefined);
      onOpenChange(false);
      toast.success(item ? "Sınıf başarıyla güncellendi." : "Sınıf başarıyla oluşturuldu.");
      router.push(classDetailHref(result.id));
      router.refresh();
    },
    onError: (error) => {
      const hasFieldErrors = applyApiFieldErrors(error, (name, message) => setError(name, { type: "server", message }), topLevelFields);
      const message = formErrorMessage(error, item ? "Sınıf kaydedilirken beklenmeyen bir hata oluştu." : "Sınıf oluşturulurken beklenmeyen bir hata oluştu.");
      if (/kapasite/i.test(message)) setError("capacity", { type: "server", message });
      setSubmitError(hasFieldErrors ? "Lütfen işaretlenen alanları kontrol edin." : message);
      toast.error(message);
    },
  });

  const submit = handleSubmit((values) => {
    if (mutation.isPending) return;
    setSubmitError(undefined);
    const room = rooms.data?.find((candidate) => candidate.id === values.studioRoomId);
    if (room?.capacity && values.capacity > room.capacity) {
      const message = `Sınıf kapasitesi ${room.name} salon kapasitesinden (${room.capacity}) büyük olamaz.`;
      setError("capacity", { type: "validate", message }, { shouldFocus: true });
      setSubmitError(message);
      return;
    }
    mutation.mutate(values);
  }, (validationErrors) => {
    setSubmitError("Form kaydedilemedi. Lütfen işaretlenen alanları kontrol edin.");
    focusFirstScheduleError(validationErrors);
  });

  const field = (id: keyof ClassFormValues, label: string, node: React.ReactNode) => <div className="space-y-2"><Label htmlFor={id}>{label}</Label>{node}{errors[id] && typeof errors[id]?.message === "string" && <FieldError id={`${id}-error`}>{errors[id]?.message}</FieldError>}</div>;

  function focusFirstScheduleError(validationErrors: FieldErrors<ClassFormValues>) {
    const rows = validationErrors.schedules;
    if (!Array.isArray(rows)) return;
    const rowIndex = rows.findIndex(Boolean);
    if (rowIndex < 0) return;
    requestAnimationFrame(() => document.querySelector<HTMLElement>(`[data-schedule-row="${rowIndex}"] select, [data-schedule-row="${rowIndex}"] input`)?.focus());
  }

  function changeOpen(nextOpen: boolean) {
    if (mutation.isPending) return;
    if (!nextOpen) setSubmitError(undefined);
    onOpenChange(nextOpen);
  }

  return <Dialog open={open} onOpenChange={changeOpen}><DialogContent className="max-h-[94dvh] overflow-y-auto sm:max-w-3xl"><DialogHeader><DialogTitle>{item ? "Sınıfı düzenle" : "Yeni sınıf"}</DialogTitle><DialogDescription>Sınıf bilgilerini ve tekrar eden haftalık programı birlikte tanımlayın.</DialogDescription></DialogHeader>
    <form id="class-form" onSubmit={submit} className="grid gap-5 sm:grid-cols-2" noValidate>
      {submitError && <div role="alert" aria-live="assertive" className="flex gap-3 rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-800 sm:col-span-2"><CircleAlert className="mt-0.5 size-4 shrink-0"/><span>{submitError}</span></div>}
      {field("name", "Sınıf adı *", <Input id="name" aria-invalid={!!errors.name} aria-describedby={errors.name ? "name-error" : undefined} autoFocus {...register("name")} />)}
      {field("status", "Durum", <select id="status" className="control" {...register("status")}>{classStatuses.map((status) => <option key={status} value={status}>{classStatusLabels[status]}</option>)}</select>)}
      {field("instructorId", "Eğitmen *", <select id="instructorId" aria-invalid={!!errors.instructorId} className="control" {...register("instructorId")}><option value="">Seçin</option>{instructors.data?.map((instructor) => <option key={instructor.id} value={instructor.id}>{instructor.fullName}</option>)}</select>)}
      {field("studioRoomId", "Stüdyo *", <select id="studioRoomId" aria-invalid={!!errors.studioRoomId} className="control" {...register("studioRoomId")}><option value="">Seçin</option>{rooms.data?.filter((room) => room.isActive && !room.isArchived).map((room) => <option key={room.id} value={room.id}>{room.name}{room.capacity ? ` · ${room.capacity} kişi` : ""}</option>)}</select>)}
      {field("capacity", "Kapasite *", <Input id="capacity" aria-invalid={!!errors.capacity} type="number" min={1} max={500} {...register("capacity", { valueAsNumber: true })} />)}
      {field("level", "Seviye", <Input id="level" {...register("level")} />)}
      {field("ageGroup", "Yaş grubu", <Input id="ageGroup" placeholder="Örn. 8–12" {...register("ageGroup")} />)}
      <div />
      {field("startDate", "Başlangıç tarihi *", <Input id="startDate" aria-invalid={!!errors.startDate} type="date" {...register("startDate")} />)}
      {field("endDate", "Bitiş tarihi", <Input id="endDate" aria-invalid={!!errors.endDate} type="date" {...register("endDate")} />)}
      <div className="space-y-2 sm:col-span-2"><Label htmlFor="description">Açıklama</Label><Textarea id="description" rows={3} {...register("description")} />{errors.description && <FieldError>{errors.description.message}</FieldError>}</div>
      <div className="sm:col-span-2"><div className="mb-3 flex items-center justify-between gap-3"><div><Label>Haftalık program</Label><p className="mt-1 text-xs text-zinc-500">Çakışan eğitmen ve stüdyo saatleri kaydedilmez.</p></div><Button type="button" size="sm" variant="outline" disabled={mutation.isPending || schedules.fields.length >= 14} onClick={() => schedules.append({ dayOfWeek: 1, startTime: "18:00", endTime: "19:00" })}><Plus />Saat ekle</Button></div>
        <div className="space-y-3">{schedules.fields.length === 0 && <p className="rounded-lg border border-dashed p-4 text-center text-sm text-zinc-500">Program satırı eklenmedi.</p>}{schedules.fields.map((row, index) => { const rowErrors = errors.schedules?.[index]; return <div key={row.id} data-schedule-row={index} className="rounded-lg border bg-zinc-50/50 p-3"><div className="grid grid-cols-[minmax(0,1fr)_96px_96px_36px] gap-2"><select aria-label={`${index + 1}. gün`} aria-invalid={!!rowErrors?.dayOfWeek} className="control px-2" {...register(`schedules.${index}.dayOfWeek`, { valueAsNumber: true })}><option value="">Gün seçin</option>{dayLabels.map((label, day) => <option key={label} value={day}>{label}</option>)}</select><Input aria-label={`${index + 1}. başlangıç saati`} aria-invalid={!!rowErrors?.startTime} type="time" {...register(`schedules.${index}.startTime`)} /><Input aria-label={`${index + 1}. bitiş saati`} aria-invalid={!!rowErrors?.endTime} type="time" {...register(`schedules.${index}.endTime`)} /><Button type="button" variant="ghost" size="icon-sm" disabled={mutation.isPending} aria-label={`${index + 1}. satırı sil`} onClick={() => schedules.remove(index)}><Trash2 /></Button></div><div className="mt-2 space-y-1">{rowErrors?.dayOfWeek && <FieldError>{rowErrors.dayOfWeek.message}</FieldError>}{rowErrors?.startTime && <FieldError>{rowErrors.startTime.message}</FieldError>}{rowErrors?.endTime && <FieldError>{rowErrors.endTime.message}</FieldError>}</div></div>})}</div>
        {errors.schedules && !Array.isArray(errors.schedules) && typeof errors.schedules.message === "string" && <FieldError>{errors.schedules.message}</FieldError>}
      </div>
    </form><DialogFooter><Button type="button" variant="outline" disabled={mutation.isPending} onClick={() => changeOpen(false)}>Vazgeç</Button><Button type="submit" form="class-form" disabled={mutation.isPending} aria-busy={mutation.isPending}>{mutation.isPending && <LoaderCircle className="animate-spin" />}{mutation.isPending ? "Kaydediliyor..." : item ? "Değişiklikleri kaydet" : "Sınıfı oluştur"}</Button></DialogFooter>
  </DialogContent></Dialog>;
}

function FieldError({ children, id }: { children: React.ReactNode; id?: string }) {
  return <p id={id} role="alert" className="text-xs text-red-600">{String(children)}</p>;
}
