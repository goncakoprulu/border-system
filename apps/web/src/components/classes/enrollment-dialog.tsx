"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { LoaderCircle, Plus, Search } from "lucide-react";
import { useDeferredValue, useState } from "react";
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
import { ClassDetail, classesApi, classKeys } from "@/lib/classes";
import { formErrorMessage } from "@/lib/form-errors";
import { StudentListItem, studentsApi } from "@/lib/students";

const today = () =>
  new Intl.DateTimeFormat("en-CA", { timeZone: "Europe/Istanbul" }).format(
    new Date(),
  );

async function loadAllStudents() {
  const firstPage = await studentsApi.list(
    new URLSearchParams({ page: "1", pageSize: "100", sortBy: "name" }),
  );
  const items = [...firstPage.items];
  for (let page = 2; page <= firstPage.totalPages; page += 1) {
    const result = await studentsApi.list(
      new URLSearchParams({
        page: String(page),
        pageSize: "100",
        sortBy: "name",
      }),
    );
    items.push(...result.items);
  }
  return items;
}

export function EnrollmentDialog({
  open,
  onOpenChange,
  item,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  item: ClassDetail;
}) {
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const deferredSearch = useDeferredValue(search.trim().toLocaleLowerCase("tr-TR"));
  const [startDate, setStartDate] = useState(today());
  const [pendingStudentId, setPendingStudentId] = useState<string>();
  const students = useQuery({
    queryKey: ["enrollment-students", "all"],
    queryFn: loadAllStudents,
    enabled: open,
  });
  const activeIds = new Set(
    item.enrollments
      .filter((enrollment) => enrollment.status === "Active")
      .map((enrollment) => enrollment.studentId),
  );
  const visibleStudents = (students.data ?? []).filter((student) =>
    matchesSearch(student, deferredSearch),
  );
  const capacityFull = activeIds.size >= item.capacity;
  const enroll = useMutation({
    mutationFn: (studentId: string) =>
      classesApi.enroll(item.id, studentId, startDate),
    onMutate: (studentId) => setPendingStudentId(studentId),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: classKeys.detail(item.id) }),
        queryClient.invalidateQueries({ queryKey: classKeys.all }),
      ]);
      toast.success("Öğrenci sınıfa eklendi.");
    },
    onError: (error) =>
      toast.error(
        formErrorMessage(
          error,
          "Öğrenci sınıfa eklenirken beklenmeyen bir hata oluştu.",
        ),
      ),
    onSettled: () => setPendingStudentId(undefined),
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-xl">
        <DialogHeader>
          <DialogTitle>Sınıfa öğrenci ekle</DialogTitle>
          <DialogDescription>
            Tüm öğrenciler aşağıda listelenir. Satırdaki artı düğmesi öğrenciyi
            doğrudan sınıfa ekler.
          </DialogDescription>
        </DialogHeader>
        <div>
          <label
            className="mb-2 block text-sm font-medium"
            htmlFor="enroll-start"
          >
            Başlangıç tarihi
          </label>
          <Input
            id="enroll-start"
            type="date"
            value={startDate}
            onChange={(event) => setStartDate(event.target.value)}
          />
        </div>
        <div className="relative">
          <Search
            className="absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400"
            size={16}
          />
          <Input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            className="pl-9"
            placeholder="Ad, telefon veya e-posta ile filtrele"
          />
        </div>
        <div className="max-h-72 divide-y overflow-y-auto rounded-lg border">
          {students.isLoading ? (
            <p className="p-4 text-sm text-zinc-500">Öğrenciler yükleniyor…</p>
          ) : students.isError ? (
            <p className="p-4 text-sm text-red-700" role="alert">
              {formErrorMessage(students.error, "Öğrenciler yüklenemedi.")}
            </p>
          ) : visibleStudents.length ? (
            visibleStudents.map((student) => {
              const enrolled = activeIds.has(student.id);
              const pending = pendingStudentId === student.id;
              const disabled =
                enrolled || capacityFull || !startDate || enroll.isPending;
              return (
                <div
                  key={student.id}
                  className="flex min-h-14 items-center gap-3 px-3 py-2"
                >
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-medium">
                      {student.firstName} {student.lastName}
                    </p>
                    <p className="truncate text-xs text-zinc-500">
                      {student.phone ?? student.email ?? "İletişim bilgisi yok"}
                    </p>
                  </div>
                  {enrolled ? (
                    <span className="shrink-0 text-xs font-medium text-zinc-500">
                      Zaten kayıtlı
                    </span>
                  ) : (
                    <Button
                      type="button"
                      variant="outline"
                      size="icon-sm"
                      disabled={disabled}
                      aria-label={`${student.firstName} ${student.lastName} öğrencisini sınıfa ekle`}
                      onClick={() => enroll.mutate(student.id)}
                    >
                      {pending ? (
                        <LoaderCircle className="animate-spin" />
                      ) : (
                        <Plus />
                      )}
                    </Button>
                  )}
                </div>
              );
            })
          ) : (
            <p className="p-4 text-sm text-zinc-500">
              {deferredSearch
                ? "Filtreyle eşleşen öğrenci bulunamadı."
                : "Listelenecek öğrenci bulunamadı."}
            </p>
          )}
        </div>
        {capacityFull && (
          <p className="text-sm font-medium text-amber-700" role="status">
            Sınıf kapasitesi dolu ({activeIds.size}/{item.capacity}).
          </p>
        )}
      </DialogContent>
    </Dialog>
  );
}

function matchesSearch(student: StudentListItem, search: string) {
  if (!search) return true;
  return [student.firstName, student.lastName, student.phone, student.email]
    .filter(Boolean)
    .some((value) => value!.toLocaleLowerCase("tr-TR").includes(search));
}
