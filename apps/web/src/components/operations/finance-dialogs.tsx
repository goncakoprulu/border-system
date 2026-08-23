"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { LoaderCircle } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { apiQuery } from "@/lib/api";
import { formErrorMessage } from "@/lib/form-errors";
import { operationKeys, operationsApi } from "@/lib/operations";

type StudentOption = { id: string; firstName: string; lastName: string };
type StudentPage = { items: StudentOption[] };
type FixedStudent = { id: string; name: string };

const today = () =>
  new Intl.DateTimeFormat("en-CA", { timeZone: "Europe/Istanbul" }).format(
    new Date(),
  );
const money = (value: number) =>
  new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY" }).format(
    value,
  );

export function MembershipDialog({
  open,
  onOpenChange,
  onSaved,
  student,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved: () => void;
  student?: FixedStudent;
}) {
  const students = useQuery({
    queryKey: ["student-options"],
    queryFn: () =>
      apiQuery<StudentPage>("/api/students?pageSize=100&status=Active"),
    enabled: open && !student,
  });
  const plans = useQuery({
    queryKey: operationKeys.section("plans"),
    queryFn: () => operationsApi.plans(),
    enabled: open,
  });
  const mutation = useMutation({
    mutationFn: (input: unknown) => operationsApi.createMembership(input),
    onSuccess: () => {
      toast.success("Üyelik oluşturuldu.");
      onOpenChange(false);
      onSaved();
    },
    onError: (error) =>
      toast.error(
        formErrorMessage(
          error,
          "Üyelik oluşturulurken beklenmeyen bir hata oluştu.",
        ),
      ),
  });
  const loadError = students.error ?? plans.error;
  return (
    <FormDialog
      open={open}
      onOpenChange={onOpenChange}
      title="Yeni üyelik ata"
      description={
        student
          ? `${student.name} için üyelik ve borç kaydı oluşturun.`
          : "Üyelik ve borç kaydını birlikte oluşturun."
      }
    >
      <form
        onSubmit={(event) => {
          event.preventDefault();
          if (mutation.isPending) return;
          const form = new FormData(event.currentTarget);
          mutation.mutate({
            studentId: student?.id ?? form.get("studentId"),
            planId: form.get("planId"),
            startDate: form.get("startDate"),
            endDate: form.get("endDate") || null,
            price: form.get("price") ? Number(form.get("price")) : null,
            discountAmount: form.get("discountAmount") ? Number(form.get("discountAmount")) : null,
            discountReason: form.get("discountReason") || null,
          });
        }}
        className="space-y-4"
      >
        {student ? (
          <ReadOnlyField label="Öğrenci" value={student.name} />
        ) : (
          <Field label="Öğrenci">
            <select name="studentId" required className="control">
              <option value="">Seçin</option>
              {students.data?.items.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.firstName} {item.lastName}
                </option>
              ))}
            </select>
          </Field>
        )}
        <Field label="Üyelik planı">
          <select name="planId" required className="control">
            <option value="">Seçin</option>
            {plans.data?.map((plan) => (
              <option key={plan.id} value={plan.id}>
                {plan.name} · {money(plan.defaultPrice)}
              </option>
            ))}
          </select>
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Başlangıç">
            <Input
              name="startDate"
              type="date"
              defaultValue={today()}
              required
            />
          </Field>
          <Field label="Bitiş">
            <Input name="endDate" type="date" />
          </Field>
        </div>
        <Field label="Özel ücret">
          <Input name="price" type="number" min="0" step="0.01" />
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="İndirim">
            <Input name="discountAmount" type="number" min="0" step="0.01" />
          </Field>
          <Field label="İndirim nedeni">
            <Input name="discountReason" maxLength={500} />
          </Field>
        </div>
        {loadError && <ErrorMessage error={loadError} />}{" "}
        {mutation.isError && <ErrorMessage error={mutation.error} />}
        <Button
          type="submit"
          className="w-full"
          disabled={mutation.isPending || !!loadError}
          aria-busy={mutation.isPending}
        >
          {mutation.isPending && <LoaderCircle className="animate-spin" />}
          {mutation.isPending ? "Kaydediliyor..." : "Üyeliği oluştur"}
        </Button>
      </form>
    </FormDialog>
  );
}

export function PaymentDialog({
  open,
  onOpenChange,
  onSaved,
  student,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved: () => void;
  student?: FixedStudent;
}) {
  const [studentId, setStudentId] = useState("");
  const selectedStudentId = student?.id ?? studentId;
  const students = useQuery({
    queryKey: ["student-options"],
    queryFn: () => apiQuery<StudentPage>("/api/students?pageSize=100"),
    enabled: open && !student,
  });
  const invoices = useQuery({
    queryKey: operationKeys.section("invoices", selectedStudentId),
    queryFn: () => operationsApi.invoices(selectedStudentId),
    enabled: open && !!selectedStudentId,
  });
  const mutation = useMutation({
    mutationFn: (input: unknown) => operationsApi.createPayment(input),
    onSuccess: () => {
      toast.success("Ödeme kaydedildi.");
      setStudentId("");
      onOpenChange(false);
      onSaved();
    },
    onError: (error) =>
      toast.error(
        formErrorMessage(
          error,
          "Ödeme kaydedilirken beklenmeyen bir hata oluştu.",
        ),
      ),
  });
  const loadError = students.error ?? invoices.error;
  const preferredInvoice = invoices.data?.[0]?.id ?? "";
  const openTotal = invoices.data?.reduce((sum, invoice) => sum + invoice.remaining, 0) ?? 0;
  const overdueTotal = invoices.data?.filter((invoice) => invoice.dueDate < today()).reduce((sum, invoice) => sum + invoice.remaining, 0) ?? 0;
  return (
    <FormDialog
      open={open}
      onOpenChange={onOpenChange}
      title="Ödeme al"
      description={
        student
          ? `${student.name} için tahsilat kaydedin.`
          : "Öğrenciyi seçin, açık borcunu görün ve tahsilatı kaydedin."
      }
    >
      <form
        onSubmit={(event) => {
          event.preventDefault();
          if (mutation.isPending) return;
          const form = new FormData(event.currentTarget);
          mutation.mutate({
            studentId: selectedStudentId,
            invoiceId: form.get("invoiceId") || null,
            amount: Number(form.get("amount")),
            paymentMethod: form.get("method"),
            notes: form.get("notes") || null,
          });
        }}
        className="space-y-4"
      >
        {student ? (
          <ReadOnlyField label="Öğrenci" value={student.name} />
        ) : (
          <Field label="1. Öğrenci">
            <select
              required
              value={studentId}
              onChange={(event) => setStudentId(event.target.value)}
              className="control"
            >
              <option value="">Seçin</option>
              {students.data?.items.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.firstName} {item.lastName}
                </option>
              ))}
            </select>
          </Field>
        )}
        <Field label="2. Açık borç">
          <select
            key={`${selectedStudentId}-${preferredInvoice}`}
            name="invoiceId"
            defaultValue={preferredInvoice}
            className="control"
          >
            <option value="">Borçla ilişkilendirme</option>
            {invoices.data?.map((invoice) => (
              <option key={invoice.id} value={invoice.id}>
                {invoice.description} · {money(invoice.remaining)}
              </option>
            ))}
          </select>
          {selectedStudentId &&
            !invoices.isLoading &&
            invoices.data?.length === 0 && (
              <p className="mt-1 text-xs text-zinc-500">
                Açık fatura bulunmuyor; ödeme serbest tahsilat olarak
                kaydedilebilir.
              </p>
            )}
        </Field>
        {selectedStudentId && invoices.data && <div className="grid grid-cols-2 gap-3 rounded-lg bg-zinc-50 p-3 text-sm"><div><p className="text-xs text-zinc-500">Toplam açık</p><p className="mt-1 font-semibold">{money(openTotal)}</p></div><div><p className="text-xs text-zinc-500">Gecikmiş</p><p className={`mt-1 font-semibold ${overdueTotal>0?"text-red-700":""}`}>{money(overdueTotal)}</p></div></div>}
        <Field label="3. Tutar">
          <Input name="amount" type="number" min="0.01" step="0.01" required />
        </Field>
        <Field label="4. Yöntem">
          <div className="grid grid-cols-3 gap-2">
            {[
              ["Cash", "Nakit"],
              ["CreditCard", "Kredi kartı"],
              ["BankTransfer", "Havale"],
            ].map(([value, label], index) => (
              <label
                key={value}
                className="cursor-pointer rounded-lg border p-3 text-center text-xs has-checked:border-[#526743] has-checked:bg-[#e9ede4]"
              >
                <input
                  className="sr-only"
                  type="radio"
                  name="method"
                  value={value}
                  defaultChecked={index === 0}
                />
                {label}
              </label>
            ))}
          </div>
        </Field>
        <Field label="Açıklama">
          <Input name="notes" />
        </Field>
        {loadError && <ErrorMessage error={loadError} />}{" "}
        {mutation.isError && <ErrorMessage error={mutation.error} />}
        <Button
          type="submit"
          className="w-full"
          disabled={mutation.isPending || !selectedStudentId || !!loadError}
          aria-busy={mutation.isPending}
        >
          {mutation.isPending && <LoaderCircle className="animate-spin" />}
          {mutation.isPending ? "Kaydediliyor..." : "Ödemeyi kaydet"}
        </Button>
      </form>
    </FormDialog>
  );
}

function FormDialog({
  open,
  onOpenChange,
  title,
  description,
  children,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description: string;
  children: React.ReactNode;
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>
        {children}
      </DialogContent>
    </Dialog>
  );
}
function Field({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div>
      <Label className="mb-2 block">{label}</Label>
      {children}
    </div>
  );
}
function ReadOnlyField({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border bg-zinc-50 px-3 py-2">
      <p className="text-xs text-zinc-500">{label}</p>
      <p className="mt-1 text-sm font-medium">{value}</p>
    </div>
  );
}
function ErrorMessage({ error }: { error: unknown }) {
  return (
    <p
      role="alert"
      className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700"
    >
      {formErrorMessage(error, "İşlem sırasında beklenmeyen bir hata oluştu.")}
    </p>
  );
}
