"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { LoaderCircle } from "lucide-react";
import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { formErrorMessage } from "@/lib/form-errors";
import { Guardian, GuardianInput, studentKeys, studentsApi } from "@/lib/students";

const schema = z.object({
  firstName: z.string().trim().min(1, "Ad zorunludur.").max(100), lastName: z.string().trim().min(1, "Soyad zorunludur.").max(100),
  relationship: z.string().trim().min(1, "Yakınlık zorunludur.").max(80), phone: z.string().max(30).optional(),
  email: z.union([z.literal(""), z.email("Geçerli bir e-posta adresi girin.")]),
});
type FormValues = z.infer<typeof schema>;

export function GuardianFormDialog({ studentId, guardian, phoneRequired = false, open, onOpenChange }: { studentId: string; guardian?: Guardian; phoneRequired?: boolean; open: boolean; onOpenChange: (open: boolean) => void }) {
  const queryClient = useQueryClient();
  const { register, reset, handleSubmit, setError, formState: { errors } } = useForm<FormValues>({ resolver: zodResolver(schema) });
  useEffect(() => { if (open) reset({ firstName: guardian?.firstName ?? "", lastName: guardian?.lastName ?? "", relationship: guardian?.relationship ?? "", phone: guardian?.phone ?? "", email: guardian?.email ?? "" }); }, [guardian, open, reset]);
  const mutation = useMutation({
    mutationFn: (values: FormValues) => {
      const input: GuardianInput = { ...values, phone: values.phone || null, email: values.email || null };
      return guardian ? studentsApi.updateGuardian(studentId, guardian.id, input) : studentsApi.addGuardian(studentId, input);
    },
    onSuccess: async () => { await queryClient.invalidateQueries({ queryKey: studentKeys.detail(studentId) }); onOpenChange(false); toast.success(guardian ? "Veli bilgileri güncellendi." : "Veli eklendi."); },
    onError: (error) => toast.error(formErrorMessage(error, "Veli bilgileri kaydedilirken beklenmeyen bir hata oluştu.")),
  });
  return <Dialog open={open} onOpenChange={onOpenChange}><DialogContent className="sm:max-w-lg"><DialogHeader><DialogTitle>{guardian ? "Veliyi düzenle" : "Veli ekle"}</DialogTitle><DialogDescription>Öğrencinin iletişim kurulabilecek veli veya yakın bilgileri.</DialogDescription></DialogHeader>
    <form id="guardian-form" className="grid gap-4 py-2 sm:grid-cols-2" onSubmit={handleSubmit((values) => {
      if (phoneRequired && !values.phone?.trim()) { setError("phone", { message: "Veli telefonu zorunludur." }); return; }
      if (phoneRequired && !/\d/.test(values.phone ?? "")) { setError("phone", { message: "Geçerli bir veli telefonu girin." }); return; }
      mutation.mutate(values);
    })}>
      <div className="space-y-2"><Label htmlFor="guardian-firstName">Ad *</Label><Input id="guardian-firstName" {...register("firstName")} />{errors.firstName && <p className="text-xs text-red-600">{errors.firstName.message}</p>}</div>
      <div className="space-y-2"><Label htmlFor="guardian-lastName">Soyad *</Label><Input id="guardian-lastName" {...register("lastName")} />{errors.lastName && <p className="text-xs text-red-600">{errors.lastName.message}</p>}</div>
      <div className="space-y-2"><Label htmlFor="relationship">Yakınlık *</Label><Input id="relationship" placeholder="Anne, baba..." {...register("relationship")} />{errors.relationship && <p className="text-xs text-red-600">{errors.relationship.message}</p>}</div>
      <div className="space-y-2"><Label htmlFor="guardian-phone">Telefon{phoneRequired ? " *" : ""}</Label><Input id="guardian-phone" inputMode="tel" {...register("phone")} />{errors.phone && <p className="text-xs text-red-600">{errors.phone.message}</p>}</div>
      <div className="space-y-2 sm:col-span-2"><Label htmlFor="guardian-email">E-posta</Label><Input id="guardian-email" type="email" {...register("email")} />{errors.email && <p className="text-xs text-red-600">{errors.email.message}</p>}</div>
    </form><DialogFooter><Button type="button" variant="outline" disabled={mutation.isPending} onClick={() => onOpenChange(false)}>Vazgeç</Button><Button type="submit" form="guardian-form" disabled={mutation.isPending} aria-busy={mutation.isPending}>{mutation.isPending && <LoaderCircle className="animate-spin" />}{mutation.isPending ? "Kaydediliyor..." : "Kaydet"}</Button></DialogFooter>
  </DialogContent></Dialog>;
}
