"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { LoaderCircle } from "lucide-react";
import { useRouter } from "next/navigation";
import { useEffect } from "react";
import { useForm, useWatch } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { applyApiFieldErrors, formErrorMessage } from "@/lib/form-errors";
import { calculateAge, StudentDetail, StudentInput, studentKeys, studentStatuses, studentStatusLabels, studentsApi } from "@/lib/students";
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
  guardianFirstName: z.string().trim().max(100).optional(),
  guardianLastName: z.string().trim().max(100).optional(),
  guardianPhone: z.string().trim().max(30).optional(),
}).superRefine((values, context) => {
  const age = calculateAge(values.birthDate || null);
  if (age === null || age >= 18) return;
  if (!values.guardianFirstName?.trim()) context.addIssue({ code: "custom", path: ["guardianFirstName"], message: "Veli adı zorunludur." });
  if (!values.guardianLastName?.trim()) context.addIssue({ code: "custom", path: ["guardianLastName"], message: "Veli soyadı zorunludur." });
  if (!values.guardianPhone?.trim()) context.addIssue({ code: "custom", path: ["guardianPhone"], message: "Veli telefonu zorunludur." });
  else if (!/\d/.test(values.guardianPhone)) context.addIssue({ code: "custom", path: ["guardianPhone"], message: "Geçerli bir veli telefonu girin." });
});
type FormValues = z.infer<typeof schema>;

const today = () => new Intl.DateTimeFormat("en-CA", { timeZone: "Europe/Istanbul" }).format(new Date());
const defaults = (student?: StudentDetail): FormValues => ({
  firstName: student?.firstName ?? "", lastName: student?.lastName ?? "", phone: student?.phone ?? "", email: student?.email ?? "",
  birthDate: student?.birthDate ?? "", gender: student?.gender ?? "", registrationDate: student?.registrationDate ?? today(),
  status: student?.status ?? "Lead", notes: student?.notes ?? "",
  guardianFirstName: student?.guardians[0]?.firstName ?? "", guardianLastName: student?.guardians[0]?.lastName ?? "",
  guardianPhone: student?.guardians[0]?.phone ?? "",
});

export function StudentFormDialog({ open, onOpenChange, student }: { open: boolean; onOpenChange: (open: boolean) => void; student?: StudentDetail }) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { register, handleSubmit, reset, setError, control, formState: { errors } } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: defaults(student) });
  useEffect(() => { if (open) reset(defaults(student)); }, [open, reset, student]);
  const birthDate = useWatch({ control, name: "birthDate" });
  const age = calculateAge(birthDate || null);
  const isMinor = age !== null && age < 18;
  const mutation = useMutation({
    mutationFn: async (values: FormValues) => {
      const input: StudentInput = {
        firstName: values.firstName, lastName: values.lastName, phone: values.phone || null, email: values.email || null, birthDate: values.birthDate || null,
        gender: values.gender || null, notes: values.notes || null,
        status: values.status, registrationDate: values.registrationDate,
        guardian: isMinor ? { id: student?.guardians[0]?.id ?? null, firstName: values.guardianFirstName ?? "", lastName: values.guardianLastName ?? "", phone: values.guardianPhone ?? "" } : null,
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
      const fields: (keyof FormValues)[] = ["firstName", "lastName", "phone", "email", "birthDate", "gender", "notes", "status", "registrationDate", "guardianFirstName", "guardianLastName", "guardianPhone"];
      applyApiFieldErrors(error, (field, message) => setError(field, { type: "server", message }), fields);
      toast.error(formErrorMessage(error, "Öğrenci kaydedilirken beklenmeyen bir hata oluştu."));
    },
  });

  const field = (id: keyof FormValues, label: string, input: React.ReactNode) => <div className="space-y-2"><Label htmlFor={id}>{label}</Label>{input}{errors[id] && <p className="text-xs text-red-600">{errors[id]?.message}</p>}</div>;

  return <Dialog open={open} onOpenChange={onOpenChange}>
    <DialogContent className="max-h-[92vh] overflow-y-auto sm:max-w-2xl">
      <DialogHeader><DialogTitle>{student ? "Öğrenciyi düzenle" : "Yeni öğrenci"}</DialogTitle><DialogDescription>Doğum tarihine göre 18 yaş altındaki öğrenciler için veli bilgileri de alınır.</DialogDescription></DialogHeader>
      <form id="student-form" className="grid gap-5 py-2 sm:grid-cols-2" onSubmit={handleSubmit((values) => mutation.mutate(values))} noValidate>
        {field("firstName", "Ad *", <Input id="firstName" autoFocus {...register("firstName")} />)}
        {field("lastName", "Soyad *", <Input id="lastName" {...register("lastName")} />)}
        {field("phone", "Telefon", <Input id="phone" inputMode="tel" placeholder="05xx xxx xx xx" {...register("phone")} />)}
        {field("email", "E-posta", <Input id="email" type="email" {...register("email")} />)}
        {field("birthDate", "Doğum tarihi", <Input id="birthDate" type="date" max={today()} {...register("birthDate")} />)}
        {field("gender", "Cinsiyet", <select id="gender" className="h-9 w-full rounded-lg border bg-transparent px-3 text-sm" {...register("gender")}><option value="">Belirtilmedi</option><option value="Kadın">Kadın</option><option value="Erkek">Erkek</option><option value="Diğer">Diğer</option></select>)}
        {field("registrationDate", "Kayıt tarihi *", <Input id="registrationDate" type="date" max={today()} {...register("registrationDate")} />)}
        {field("status", "Durum", <select id="status" className="h-9 w-full rounded-lg border bg-transparent px-3 text-sm" {...register("status")}>{studentStatuses.map((status) => <option key={status} value={status}>{studentStatusLabels[status]}</option>)}</select>)}
        {isMinor && <fieldset className="grid gap-5 rounded-lg border p-4 sm:col-span-2 sm:grid-cols-2"><legend className="px-2 text-sm font-medium">Veli bilgileri · {age} yaş</legend>
          {field("guardianFirstName", "Veli adı *", <Input id="guardianFirstName" {...register("guardianFirstName")} />)}
          {field("guardianLastName", "Veli soyadı *", <Input id="guardianLastName" {...register("guardianLastName")} />)}
          <div className="sm:col-span-2">{field("guardianPhone", "Veli telefonu *", <Input id="guardianPhone" inputMode="tel" placeholder="05xx xxx xx xx" {...register("guardianPhone")} />)}</div>
        </fieldset>}
        <div className="space-y-2 sm:col-span-2"><Label htmlFor="notes">Notlar</Label><Textarea id="notes" rows={4} {...register("notes")} />{errors.notes && <p className="text-xs text-red-600">{errors.notes.message}</p>}</div>
      </form>
      <DialogFooter><Button type="button" variant="outline" disabled={mutation.isPending} onClick={() => onOpenChange(false)}>Vazgeç</Button><Button type="submit" form="student-form" disabled={mutation.isPending} aria-busy={mutation.isPending}>{mutation.isPending && <LoaderCircle className="animate-spin" />}{mutation.isPending ? "Kaydediliyor..." : student ? "Değişiklikleri kaydet" : "Öğrenciyi oluştur"}</Button></DialogFooter>
    </DialogContent>
  </Dialog>;
}
