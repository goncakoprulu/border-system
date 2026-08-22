import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const reports=readFileSync("src/components/operations/reports-section.tsx","utf8");
const api=readFileSync("src/lib/reports.ts","utf8");
const management=readFileSync("src/components/operations/management-section.tsx","utf8");

for(const preset of ["Bu ay","Geçen ay","Son 30 gün","Son 3 ay","Bu yıl","Özel tarih aralığı"]) assert.ok(reports.includes(preset),`${preset} filtresi bulunmalı`);
for(const filter of ["Eğitmen","Sınıf","Stüdyo"]) assert.ok(reports.includes(`label=\"${filter}\"`),`${filter} filtresi bulunmalı`);
for(const title of ["Tahsilat raporu","Borç ve faturalar","Devam ve yoklama","Öğrenci raporu","Sınıf dolulukları","Eğitmen operasyon özeti","Üyelik raporu"]) assert.ok(reports.includes(title),`${title} bölümü bulunmalı`);
assert.ok(reports.includes("trendPercent")&&reports.includes("önceki döneme göre"),"KPI trendleri gösterilmeli");
assert.ok(reports.includes("studentDetailHref")&&reports.includes("classDetailHref")&&reports.includes('href="/balances"')&&reports.includes('href="/attendance"'),"drill-down bağlantıları bulunmalı");
assert.ok(reports.includes("\\uFEFF")&&reports.includes('text/csv;charset=utf-8'),"CSV UTF-8 BOM ile üretilmeli");
assert.ok(reports.includes("grid-cols-2")&&reports.includes("lg:grid-cols-2"),"rapor düzeni mobil tek kolon ve desktop iki kolon olmalı");
assert.ok(reports.includes("SectionError")&&reports.includes("Tekrar dene")&&reports.includes("SectionSkeleton")&&reports.includes("Empty"),"bölüm state'leri bulunmalı");
for(const endpoint of ["summary","finance","engagement","capacity"]) assert.ok(api.includes(`/api/reports/${endpoint}`),`${endpoint} endpointi kullanılmalı`);
assert.ok(management.includes("<ReportsSection />")&&!management.includes("function Reports()"),"eski rapor placeholder'ı kaldırılmalı");

console.log("reports regression checks passed");
