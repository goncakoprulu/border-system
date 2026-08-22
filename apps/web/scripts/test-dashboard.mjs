import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const dashboard=readFileSync("src/app/(protected)/dashboard/page.tsx","utf8");
const operations=readFileSync("src/lib/operations.ts","utf8");
const attendance=readFileSync("src/components/operations/attendance-section.tsx","utf8");

assert.ok(!dashboard.includes("BORDER yönetim sistemi hazır"));
assert.ok(!dashboard.includes("sonraki fazlarda"));
for(const text of ["Aktif öğrenci","Bugünkü ders","Bu ay tahsilat","Açık bakiye","Bugünün programı","Dikkat gerekenler","Son 30 gün","Hızlı işlemler","Öğrenci ekle","Ödeme al","Üyelik oluştur","Sınıfa öğrenci ata","Yoklama aç","Tekrar dene"]) assert.ok(dashboard.includes(text),`${text} dashboard'da bulunmalı`);
assert.ok(dashboard.includes("grid-cols-2")&&dashboard.includes("xl:grid-cols-4"),"KPI alanı mobil 2x2 ve desktop 4 kolon olmalı");
assert.ok(dashboard.includes("order-2 lg:order-1")&&dashboard.includes("order-1 lg:order-2"),"mobilde hızlı işlemler grafiklerden önce olmalı");
assert.ok(dashboard.includes("conic-gradient")&&dashboard.includes("thirtyDayRevenue"),"tahsilat ve devam grafikleri bulunmalı");
assert.ok(dashboard.includes("StudentFormDialog")&&dashboard.includes("PaymentDialog")&&dashboard.includes("MembershipDialog")&&dashboard.includes("StudentClassAssignmentDialog"));
assert.ok(operations.includes("/api/dashboard/operations")&&operations.includes("/api/dashboard/analytics"));
assert.ok(attendance.includes('searchParams.get("sessionId")'),"dashboard yoklama aksiyonu oturumu doğrudan açmalı");

console.log("dashboard regression checks passed");
