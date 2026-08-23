import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";

const read = (file) => fs.readFileSync(path.resolve(file), "utf8");
const search = read("src/components/global-search.tsx");
for (const expected of ["Ctrl K", "En az 2 karakter", "operationsApi.search", "Eşleşen kayıt bulunamadı", "results.isError", "Tekrar dene"]) {
  assert.ok(search.includes(expected), `Hızlı arama davranışı eksik: ${expected}`);
}

const shell = read("src/components/app-shell.tsx");
assert.ok(shell.includes("<GlobalSearch />"), "Hızlı arama uygulama kabuğuna eklenmedi.");

const management = read("src/components/operations/management-section.tsx");
for (const expected of ["changeMembershipStatus", "Dondur", "Aktifleştir", "Finans geçmişi korunacaktır", "Bilinmeyen durum"]) {
  assert.ok(management.includes(expected), `Üyelik yaşam döngüsü davranışı eksik: ${expected}`);
}

const attendance = read("src/components/operations/attendance-section.tsx");
for (const expected of ["recentSessionCount", "recentAbsenceCount", "devamsızlık", "Öğrenci notu:"]) {
  assert.ok(attendance.includes(expected), `Yoklama risk göstergesi eksik: ${expected}`);
}

const finance = read("src/components/operations/finance-dialogs.tsx");
for (const expected of ["discountAmount", "discountReason", "Toplam açık", "Gecikmiş"]) {
  assert.ok(finance.includes(expected), `Finans formu davranışı eksik: ${expected}`);
}

const operations = read("../../src/Border.Infrastructure/Operations/OperationsService.cs");
for (const expected of ["IsolationLevel.Serializable", "EmptyClasses", "LowAttendance", "UnassignedStudents", "AttendanceSaved", "PaymentCreated", "MembershipStatusChanged"]) {
  assert.ok(operations.includes(expected), `Backend operasyon koruması eksik: ${expected}`);
}

const classes = read("../../src/Border.Infrastructure/Classes/ClassService.cs");
assert.ok(classes.includes("IsolationLevel.Serializable"), "Sınıf kayıt işlemi atomik değil.");

console.log("operational audit regression tests passed");
