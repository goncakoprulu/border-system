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
import { apiQuery } from "@/lib/api";
import { classesApi, classKeys } from "@/lib/classes";
import { formErrorMessage } from "@/lib/form-errors";
import { studentKeys } from "@/lib/students";

type StudentPage = {
  items: { id: string; firstName: string; lastName: string }[];
};

const today = () =>
  new Intl.DateTimeFormat("en-CA", { timeZone: "Europe/Istanbul" }).format(
    new Date(),
  );

export function StudentClassAssignmentDialog({
  student,
  open,
  onOpenChange,
}: {
  student?: { id: string; name: string };
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
  const students = useQuery({
    queryKey: ["student-options"],
    queryFn: () =>
      apiQuery<StudentPage>("/api/students?pageSize=100&status=Active"),
    enabled: open && !student,
  });
  const mutation = useMutation({
    mutationFn: ({
      classId,
      startDate,
      studentId,
    }: {
      classId: string;
      startDate: string;
      studentId: string;
    }) => classesApi.enroll(classId, studentId, startDate),
    onSuccess: async (_, input) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: studentKeys.all }),
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
            {student
              ? `${student.name} için aktif bir sınıf ve başlangıç tarihi seçin.`
              : "Aktif bir öğrenci, sınıf ve başlangıç tarihi seçin."}
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
              studentId: student?.id ?? String(form.get("studentId")),
            });
          }}
        >
          {!student && (
            <div>
              <Label className="mb-2 block" htmlFor="assign-student">
                Öğrenci
              </Label>
              <select
                id="assign-student"
                name="studentId"
                required
                className="control"
              >
                <option value="">Seçin</option>
                {students.data?.items.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.firstName} {item.lastName}
                  </option>
                ))}
              </select>
            </div>
          )}
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
          {(classes.isError || students.isError) && (
            <ErrorMessage error={classes.error ?? students.error} />
          )}{" "}
          {mutation.isError && <ErrorMessage error={mutation.error} />}
          <Button
            className="w-full"
            type="submit"
            disabled={
              classes.isLoading ||
              students.isLoading ||
              classes.isError ||
              students.isError ||
              mutation.isPending
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
