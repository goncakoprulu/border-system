import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const balances=readFileSync("src/components/operations/balances-section.tsx","utf8");
const operations=readFileSync("src/lib/operations.ts","utf8");
const management=readFileSync("src/components/operations/management-section.tsx","utf8");

for(const kpi of ["Toplam açık bakiye","Gecikmiş bakiye","Borçlu öğrenci","Bu ay tahsil edilen"]) assert.ok(balances.includes(kpi),`${kpi} KPI bulunmalı`);
for(const filter of ["Öğrenci ara","Sadece gecikmiş borçlular","Açık bakiye > 0","Borcu kapanmışları göster"]) assert.ok(balances.includes(filter),`${filter} filtresi bulunmalı`);
for(const column of ["Toplam borç","Ödenen","Açık bakiye","Gecikmiş","Son ödeme","Durum"]) assert.ok(balances.includes(column),`${column} alanı bulunmalı`);
for(const status of ["Borç yok","Açık bakiye","Gecikmiş"]) assert.ok(balances.includes(status),`${status} durumu bulunmalı`);
assert.ok(balances.includes("ListSkeleton")&&balances.includes("ErrorState")&&balances.includes("EmptyState")&&balances.includes("Tekrar dene"),"loading, error, retry ve empty state bulunmalı");
assert.ok(balances.includes("InvoiceDrilldown")&&balances.includes("operationsApi.invoices")&&balances.includes("Ödeme al")&&balances.includes("PaymentDialog"),"fatura drill-down ve ödeme aksiyonu bulunmalı");
assert.ok(balances.includes("studentDetailHref")&&balances.includes("router.push"),"satır öğrenci detayına yönlenmeli");
assert.ok(balances.includes("md:hidden")&&balances.includes("hidden overflow-x-auto md:block"),"mobil kart ve masaüstü tablo düzeni bulunmalı");
for(const parameter of ["search","overdueOnly","openOnly","includeSettled"]) assert.ok(operations.includes(`params.set(\"${parameter}\"`),`${parameter} API filtresi iletilmeli`);
assert.ok(management.includes("<BalancesSection />")&&!management.includes("function Balances()"),"eski balance görünümü kaldırılmalı");

console.log("balance dashboard regression checks passed");
