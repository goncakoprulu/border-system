import { readFileSync } from "node:fs";
import assert from "node:assert/strict";

const detail = readFileSync("src/app/(protected)/students/detail/student-detail-client.tsx", "utf8");
const operations = readFileSync("src/lib/operations.ts", "utf8");
const students = readFileSync("src/lib/students.ts", "utf8");
const attendance = readFileSync("src/components/operations/attendance-section.tsx", "utf8");
const studentList = readFileSync("src/app/(protected)/students/page.tsx", "utf8");
const enrollmentDialog = readFileSync("src/components/classes/enrollment-dialog.tsx", "utf8");

for (const text of ["Sınıfa ata", "Üyelik ata", "Ödeme al", "Veli ekle", "Son 10 yoklama", "Son faturalar", "Tekrar dene"]) assert.ok(detail.includes(text), `${text} çalışma alanında bulunmalı`);
assert.ok(detail.includes('operationsApi.studentFinance(id)'));
assert.ok(detail.includes('operationsApi.studentAttendance(id)'));
assert.ok(detail.includes('item.enrollmentId'), "sınıf işlemleri backend enrollmentId alanını kullanmalı");
assert.ok(!detail.includes("bu fazda uygulanmadı"));
assert.ok(!detail.includes("henüz bu ekrana bağlı değil"));
assert.ok(operations.includes("/finance-overview") && operations.includes("/attendance-history"));
assert.ok(students.includes("enrollmentId: string"));
assert.ok(attendance.includes('searchParams.get("studentId")'));
assert.ok(studentList.includes("StudentClassAssignmentDialog"));
assert.ok(studentList.includes("Sınıfa ekle"));
assert.ok(enrollmentDialog.includes("loadAllStudents"));
assert.ok(enrollmentDialog.includes("enroll.mutate(student.id)"));
assert.ok(enrollmentDialog.includes("Zaten kayıtlı"));

console.log("Student workspace regression checks passed.");
