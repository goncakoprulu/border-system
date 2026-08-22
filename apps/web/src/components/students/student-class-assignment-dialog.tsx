"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { LoaderCircle } from "lucide-react";
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
import { classesApi, classKeys } from "@/lib/classes";
import { formErrorMessage } from "@/lib/form-errors";
import { studentKeys } from "@/lib/students";

const today = () =>
  new Intl.DateTimeFormat("en-CA", { timeZone: "Europe/Istanbul" }).format(
    new Date(),
  );

export function StudentClassAssignmentDialog({
  student,
  open,
  onOpenChange,
}: {
  student: { id: string; name: string };
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const queryClient = useQueryClient();
  const classes = useQuery({
    queryKey: classKeys.list("student-assignment"),
    queryFn: () =>
      classesApi.list(
        new URLSearchParams({ pageSize: "100", status: "Active" }),
      ),
    enabled: open,
  });
  const mutation = useMutation({
    mutationFn: ({
      classId,
      startDate,
    }: {
      classId: string;
      startDate: string;
    }) => classesApi.enroll(classId, student.id, startDate),
    onSuccess: async (_, input) => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: studentKeys.detail(student.id),
        }),
        queryClient.invalidateQueries({
          queryKey: classKeys.detail(input.classId),
        }),
        queryClient.invalidateQueries({ queryKey: classKeys.all }),
      ]);
      toast.success("Öğrenci sınıfa kaydedildi.");
      onOpenChange(false);
    },
    onError: (error) =>
      toast.error(
        formErrorMessage(
          error,
          "Sınıf kaydı oluşturulurken beklenmeyen bir hata oluştu.",
        ),
      ),
  });
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Sınıfa ata</DialogTitle>
          <DialogDescription>
            {student.name} için aktif bir sınıf ve başlangıç tarihi seçin.
          </DialogDescription>
        </DialogHeader>
        <form
          className="space-y-4"
          onSubmit={(event) => {
            event.preventDefault();
            if (mutation.isPending) return;
            const form = new FormData(event.currentTarget);
            mutation.mutate({
              classId: String(form.get("classId")),
              startDate: String(form.get("startDate")),
            });
          }}
        >
          <div>
            <Label className="mb-2 block" htmlFor="assign-class">
              Sınıf
            </Label>
            <select
              id="assign-class"
              name="classId"
              required
              className="control"
            >
              <option value="">Seçin</option>
              {classes.data?.items.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.name} · {item.instructorName}
                </option>
              ))}
            </select>
          </div>
          <div>
            <Label className="mb-2 block" htmlFor="assign-start-date">
              Başlangıç tarihi
            </Label>
            <Input
              id="assign-start-date"
              name="startDate"
              type="date"
              defaultValue={today()}
              required
            />
          </div>
          {classes.isError && <ErrorMessage error={classes.error} />}{" "}
          {mutation.isError && <ErrorMessage error={mutation.error} />}
          <Button
            className="w-full"
            type="submit"
            disabled={
              classes.isLoading || classes.isError || mutation.isPending
            }
            aria-busy={mutation.isPending}
          >
            {mutation.isPending && <LoaderCircle className="animate-spin" />}
            {mutation.isPending ? "Kaydediliyor..." : "Sınıfa kaydet"}
          </Button>
        </form>
      </DialogContent>
    </Dialog>
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
