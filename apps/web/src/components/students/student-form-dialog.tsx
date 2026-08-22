"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { LoaderCircle } from "lucide-react";
import { useRouter } from "next/navigation";
import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { applyApiFieldErrors, formErrorMessage } from "@/lib/form-errors";
import { StudentDetail, StudentInput, studentKeys, studentStatuses, studentStatusLabels, studentsApi } from "@/lib/students";
import { studentDetailHref } from "@/lib/routes";

const schema = z.object({
  firstName: z.string().trim().min(1, "Ad zorunludur.").max(100),
  lastName: z.string().trim().min(1, "Soyad zorunludur.").max(100),
  phone: z.string().trim().max(30).optional(),
  email: z.union([z.literal(""), z.email("Geçerli bir e-posta adresi girin.")]),
  birthDate: z.string().optional(),
  gender: z.string().max(30).optional(),
  registrationDate: z.string().min(1, "Kayıt tarihi zorunludur."),
  status: z.enum(studentStatuses),
  notes: z.string().max(2000).optional(),
});
type FormValues = z.infer<typeof schema>;

const today = () => new Intl.DateTimeFormat("en-CA", { timeZone: "Europe/Istanbul" }).format(new Date());
const defaults = (student?: StudentDetail): FormValues => ({
  firstName: student?.firstName ?? "", lastName: student?.lastName ?? "", phone: student?.phone ?? "", email: student?.email ?? "",
  birthDate: student?.birthDate ?? "", gender: student?.gender ?? "", registrationDate: student?.registrationDate ?? today(),
  status: student?.status ?? "Lead", notes: student?.notes ?? "",
});

export function StudentFormDialog({ open, onOpenChange, student }: { open: boolean; onOpenChange: (open: boolean) => void; student?: StudentDetail }) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { register, handleSubmit, reset, setError, formState: { errors } } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: defaults(student) });
  useEffect(() => { if (open) reset(defaults(student)); }, [open, reset, student]);
  const mutation = useMutation({
    mutationFn: async (values: FormValues) => {
      const input: StudentInput = {
        ...values, phone: values.phone || null, email: values.email || null, birthDate: values.birthDate || null,
        gender: values.gender || null, notes: values.notes || null,
      };
      return student ? studentsApi.update(student.id, input) : studentsApi.create(input);
    },
    onSuccess: async (result) => {
      await queryClient.invalidateQueries({ queryKey: studentKeys.all });
      onOpenChange(false);
      if (student) {
        toast.success("Öğrenci bilgileri güncellendi.");
      } else if ("student" in result) {
        if (result.duplicateWarnings.length > 0) toast.warning("Benzer öğrenci kaydı bulundu.", { description: result.duplicateWarnings.map((item) => item.fullName).join(", ") });
        else toast.success("Öğrenci oluşturuldu.");
        router.push(studentDetailHref(result.student.id));
      }
    },
    onError: (error) => {
      const fields: (keyof FormValues)[] = ["firstName", "lastName", "phone", "email", "birthDate", "gender", "notes", "status", "registrationDate"];
      applyApiFieldErrors(error, (field, message) => setError(field, { type: "server", message }), fields);
      toast.error(formErrorMessage(error, "Öğrenci kaydedilirken beklenmeyen bir hata oluştu."));
    },
  });

  const field = (id: keyof FormValues, label: string, input: React.ReactNode) => <div className="space-y-2"><Label htmlFor={id}>{label}</Label>{input}{errors[id] && <p className="text-xs text-red-600">{errors[id]?.message}</p>}</div>;

  return <Dialog open={open} onOpenChange={onOpenChange}>
    <DialogContent className="max-h-[92vh] overflow-y-auto sm:max-w-2xl">
      <DialogHeader><DialogTitle>{student ? "Öğrenciyi düzenle" : "Yeni öğrenci"}</DialogTitle><DialogDescription>Temel öğrenci bilgilerini girin. Veli bilgileri öğrenci kaydedildikten sonra eklenebilir.</DialogDescription></DialogHeader>
      <form id="student-form" className="grid gap-5 py-2 sm:grid-cols-2" onSubmit={handleSubmit((values) => mutation.mutate(values))} noValidate>
        {field("firstName", "Ad *", <Input id="firstName" autoFocus {...register("firstName")} />)}
        {field("lastName", "Soyad *", <Input id="lastName" {...register("lastName")} />)}
        {field("phone", "Telefon", <Input id="phone" inputMode="tel" placeholder="05xx xxx xx xx" {...register("phone")} />)}
        {field("email", "E-posta", <Input id="email" type="email" {...register("email")} />)}
        {field("birthDate", "Doğum tarihi", <Input id="birthDate" type="date" max={today()} {...register("birthDate")} />)}
        {field("gender", "Cinsiyet", <select id="gender" className="h-9 w-full rounded-lg border bg-transparent px-3 text-sm" {...register("gender")}><option value="">Belirtilmedi</option><option value="Kadın">Kadın</option><option value="Erkek">Erkek</option><option value="Diğer">Diğer</option></select>)}
        {field("registrationDate", "Kayıt tarihi *", <Input id="registrationDate" type="date" max={today()} {...register("registrationDate")} />)}
        {field("status", "Durum", <select id="status" className="h-9 w-full rounded-lg border bg-transparent px-3 text-sm" {...register("status")}>{studentStatuses.map((status) => <option key={status} value={status}>{studentStatusLabels[status]}</option>)}</select>)}
        <div className="space-y-2 sm:col-span-2"><Label htmlFor="notes">Notlar</Label><Textarea id="notes" rows={4} {...register("notes")} />{errors.notes && <p className="text-xs text-red-600">{errors.notes.message}</p>}</div>
      </form>
      <DialogFooter><Button type="button" variant="outline" disabled={mutation.isPending} onClick={() => onOpenChange(false)}>Vazgeç</Button><Button type="submit" form="student-form" disabled={mutation.isPending} aria-busy={mutation.isPending}>{mutation.isPending && <LoaderCircle className="animate-spin" />}{mutation.isPending ? "Kaydediliyor..." : student ? "Değişiklikleri kaydet" : "Öğrenciyi oluştur"}</Button></DialogFooter>
    </DialogContent>
  </Dialog>;
}
