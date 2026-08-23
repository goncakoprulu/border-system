"use client";

import { useQuery, useQueryClient } from "@tanstack/react-query";
import { AlertTriangle, Banknote, ChevronRight, ReceiptText, RotateCcw, Search, WalletCards } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useDeferredValue, useState } from "react";
import { PaymentDialog } from "@/components/operations/finance-dialogs";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { formErrorMessage } from "@/lib/form-errors";
import { Balance, DebtStatus, operationKeys, operationsApi } from "@/lib/operations";
import { studentDetailHref } from "@/lib/routes";

const money = (value: number) => new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY" }).format(value);
const shortDate = (value: string | null) => value ? new Intl.DateTimeFormat("tr-TR", { dateStyle: "medium", timeZone: "Europe/Istanbul" }).format(new Date(value)) : "Henüz ödeme yok";
const dueDate = (value: string) => new Intl.DateTimeFormat("tr-TR", { dateStyle: "medium" }).format(new Date(`${value}T12:00:00`));

const statusMeta: Record<DebtStatus, { label: string; className: string }> = {
  None: { label: "Borç yok", className: "bg-emerald-50 text-emerald-700 ring-emerald-200" },
  Open: { label: "Açık bakiye", className: "bg-amber-50 text-amber-700 ring-amber-200" },
  Overdue: { label: "Gecikmiş", className: "bg-rose-50 text-rose-700 ring-rose-200" },
};

export function BalancesSection() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [overdueOnly, setOverdueOnly] = useState(false);
  const [openOnly, setOpenOnly] = useState(false);
  const [includeSettled, setIncludeSettled] = useState(false);
  const [selected, setSelected] = useState<Balance | null>(null);
  const [paymentOpen, setPaymentOpen] = useState(false);
  const deferredSearch = useDeferredValue(search);
  const filters = { search: deferredSearch, overdueOnly, openOnly, includeSettled };
  const query = useQuery({
    queryKey: operationKeys.section("balances", JSON.stringify(filters)),
    queryFn: () => operationsApi.balances(filters),
  });

  const openStudent = (studentId: string) => router.push(studentDetailHref(studentId));
  const invalidateFinance = () => {
    queryClient.invalidateQueries({ queryKey: ["operations"] });
    if (selected) {
      queryClient.invalidateQueries({ queryKey: operationKeys.section("invoices", selected.studentId) });
      queryClient.invalidateQueries({ queryKey: operationKeys.section("student-finance", selected.studentId) });
    }
  };

  return (
    <>
      <Kpis loading={query.isLoading} summary={query.data?.summary} />

      <section className="mb-4 rounded-xl border bg-white p-4 shadow-sm">
        <div className="relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400" size={17} />
          <Input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Öğrenci ara..." className="pl-9" aria-label="Öğrenci ara" />
        </div>
        <div className="mt-3 flex flex-wrap gap-x-5 gap-y-2 text-sm">
          <FilterToggle checked={overdueOnly} onChange={setOverdueOnly} label="Sadece gecikmiş borçlular" />
          <FilterToggle checked={openOnly} onChange={setOpenOnly} label="Açık bakiye > 0" />
          <FilterToggle checked={includeSettled} onChange={setIncludeSettled} label="Borcu kapanmışları göster" />
        </div>
      </section>

      <section className="overflow-hidden rounded-xl border bg-white shadow-sm">
        {query.isLoading ? (
          <ListSkeleton />
        ) : query.isError ? (
          <ErrorState error={query.error} retry={() => query.refetch()} />
        ) : !query.data?.items.length ? (
          <EmptyState filtered={Boolean(search || overdueOnly || openOnly || includeSettled)} />
        ) : (
          <>
            <div className="hidden overflow-x-auto md:block">
              <Table>
                <TableHeader>
                  <TableRow>
                    {['Öğrenci','Toplam borç','Ödenen','Açık bakiye','Gecikmiş','Son ödeme','Durum',''].map((header) => <TableHead key={header}>{header}</TableHead>)}
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {query.data.items.map((item) => (
                    <TableRow key={item.studentId} className="cursor-pointer hover:bg-zinc-50" tabIndex={0} onClick={() => openStudent(item.studentId)} onKeyDown={(event) => { if (event.key === "Enter") openStudent(item.studentId); }}>
                      <TableCell className="font-medium">{item.studentName}</TableCell>
                      <TableCell>{money(item.totalDebt)}</TableCell>
                      <TableCell className="text-emerald-700">{money(item.paid)}</TableCell>
                      <TableCell className="font-semibold">{money(item.remaining)}</TableCell>
                      <TableCell className={item.overdueBalance > 0 ? "font-semibold text-rose-700" : "text-zinc-500"}>{money(item.overdueBalance)}</TableCell>
                      <TableCell>{shortDate(item.lastPaymentDate)}</TableCell>
                      <TableCell><StatusBadge status={item.status} /></TableCell>
                      <TableCell className="text-right">
                        <Button size="sm" variant="outline" onClick={(event) => { event.stopPropagation(); setSelected(item); }}>Faturalar</Button>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
            <div className="divide-y md:hidden">
              {query.data.items.map((item) => (
                <article key={item.studentId} role="button" tabIndex={0} className="p-4 active:bg-zinc-50" onClick={() => openStudent(item.studentId)} onKeyDown={(event) => { if (event.key === "Enter") openStudent(item.studentId); }}>
                  <div className="flex items-start justify-between gap-3"><div><p className="font-semibold">{item.studentName}</p><p className="mt-1 text-xs text-zinc-500">Son ödeme: {shortDate(item.lastPaymentDate)}</p></div><StatusBadge status={item.status} /></div>
                  <div className="mt-4 grid grid-cols-2 gap-3 text-sm"><Amount label="Toplam borç" value={item.totalDebt} /><Amount label="Ödenen" value={item.paid} /><Amount label="Açık bakiye" value={item.remaining} strong /><Amount label="Gecikmiş" value={item.overdueBalance} overdue={item.overdueBalance > 0} /></div>
                  <Button className="mt-4 w-full" size="sm" variant="outline" onClick={(event) => { event.stopPropagation(); setSelected(item); }}>Faturaları görüntüle <ChevronRight /></Button>
                </article>
              ))}
            </div>
          </>
        )}
      </section>

      <InvoiceDrilldown balance={selected} onClose={() => setSelected(null)} onPayment={() => setPaymentOpen(true)} />
      <PaymentDialog open={paymentOpen} onOpenChange={setPaymentOpen} onSaved={invalidateFinance} student={selected ? { id: selected.studentId, name: selected.studentName } : undefined} />
    </>
  );
}

function Kpis({ loading, summary }: { loading: boolean; summary?: { openBalance:number; debtorCount:number; collectedThisMonth:number; overdueTotal:number } }) {
  const items = [
    { label: "Toplam açık bakiye", value: money(summary?.openBalance ?? 0), icon: WalletCards, tone: "text-amber-700 bg-amber-50" },
    { label: "Gecikmiş bakiye", value: money(summary?.overdueTotal ?? 0), icon: AlertTriangle, tone: "text-rose-700 bg-rose-50" },
    { label: "Borçlu öğrenci", value: String(summary?.debtorCount ?? 0), icon: ReceiptText, tone: "text-zinc-700 bg-zinc-100" },
    { label: "Bu ay tahsil edilen", value: money(summary?.collectedThisMonth ?? 0), icon: Banknote, tone: "text-emerald-700 bg-emerald-50" },
  ];
  return <div className="mb-5 grid grid-cols-2 gap-3 xl:grid-cols-4">{items.map(({label,value,icon:Icon,tone}) => <section key={label} className="rounded-xl border bg-white p-4 shadow-sm"><div className={`mb-3 grid size-9 place-items-center rounded-lg ${tone}`}><Icon size={18} /></div><p className="text-xs font-medium text-zinc-500">{label}</p>{loading ? <div className="mt-2 h-7 w-24 animate-pulse rounded bg-zinc-100" /> : <p className="mt-1 text-xl font-bold tracking-tight sm:text-2xl">{value}</p>}</section>)}</div>;
}

function InvoiceDrilldown({ balance, onClose, onPayment }: { balance: Balance | null; onClose: () => void; onPayment: () => void }) {
  const invoices = useQuery({ queryKey: operationKeys.section("invoices", balance?.studentId ?? ""), queryFn: () => operationsApi.invoices(balance!.studentId), enabled: Boolean(balance) });
  return <Dialog open={Boolean(balance)} onOpenChange={(open) => { if (!open) onClose(); }}><DialogContent className="max-h-[88vh] overflow-y-auto sm:max-w-3xl"><DialogHeader><DialogTitle>{balance?.studentName} · Açık faturalar</DialogTitle><DialogDescription>Fatura bazında tahsilat ve kalan borç bilgileri.</DialogDescription></DialogHeader>
    {balance && <div className="grid grid-cols-2 gap-3 sm:grid-cols-4"><Amount label="Toplam borç" value={balance.totalDebt} /><Amount label="Ödenen" value={balance.paid} /><Amount label="Açık" value={balance.remaining} strong /><Amount label="Gecikmiş" value={balance.overdueBalance} overdue={balance.overdueBalance > 0} /></div>}
    {invoices.isLoading ? <ListSkeleton compact /> : invoices.isError ? <ErrorState error={invoices.error} retry={() => invoices.refetch()} /> : invoices.data?.length ? <div className="overflow-x-auto rounded-lg border"><Table><TableHeader><TableRow>{['Fatura','Tutar','Ödenen','Kalan','Vade','Durum'].map((header) => <TableHead key={header}>{header}</TableHead>)}</TableRow></TableHeader><TableBody>{invoices.data.map((invoice) => <TableRow key={invoice.id}><TableCell className="font-medium">{invoice.description}</TableCell><TableCell>{money(invoice.amount)}</TableCell><TableCell>{money(invoice.paid)}</TableCell><TableCell className="font-semibold">{money(invoice.remaining)}</TableCell><TableCell>{dueDate(invoice.dueDate)}</TableCell><TableCell><span className="text-xs font-medium text-amber-700">{invoice.status === "PartiallyPaid" ? "Kısmi ödendi" : "Bekliyor"}</span></TableCell></TableRow>)}</TableBody></Table></div> : <div className="rounded-lg border border-dashed p-6 text-center text-sm text-zinc-500">Ödenmemiş fatura bulunmuyor.</div>}
    <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-between"><Button variant="outline" render={<Link href={balance ? studentDetailHref(balance.studentId) : "/students"} />}>Öğrenci detayına git</Button><Button onClick={onPayment}><Banknote /> Ödeme al</Button></div>
  </DialogContent></Dialog>;
}

function FilterToggle({ checked, onChange, label }: { checked:boolean; onChange:(checked:boolean)=>void; label:string }) { return <label className="flex cursor-pointer items-center gap-2 text-zinc-700"><input type="checkbox" checked={checked} onChange={(event) => onChange(event.target.checked)} className="size-4 accent-[#526743]" /><span>{label}</span></label>; }
function StatusBadge({ status }: { status: DebtStatus }) { const meta=statusMeta[status]; return <span className={`inline-flex whitespace-nowrap rounded-full px-2.5 py-1 text-xs font-semibold ring-1 ring-inset ${meta.className}`}>{meta.label}</span>; }
function Amount({ label, value, strong=false, overdue=false }: { label:string; value:number; strong?:boolean; overdue?:boolean }) { return <div><p className="text-xs text-zinc-500">{label}</p><p className={`mt-1 ${strong?'font-bold':'font-medium'} ${overdue?'text-rose-700':''}`}>{money(value)}</p></div>; }
function ListSkeleton({ compact=false }: { compact?:boolean }) { return <div className="space-y-3 p-4" aria-label="Borç bilgileri yükleniyor">{Array.from({length:compact?2:5}).map((_,index) => <div key={index} className="h-14 animate-pulse rounded-lg bg-zinc-100" />)}</div>; }
function ErrorState({ error, retry }: { error:unknown; retry:()=>void }) { return <div className="p-8 text-center"><AlertTriangle className="mx-auto text-rose-600" /><p className="mt-3 font-semibold">Borç bilgileri yüklenemedi</p><p className="mx-auto mt-1 max-w-lg text-sm text-zinc-500">{formErrorMessage(error,"Borç bilgileri yüklenirken beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.")}</p><Button className="mt-4" variant="outline" onClick={retry}><RotateCcw /> Tekrar dene</Button></div>; }
function EmptyState({ filtered }: { filtered:boolean }) { return <div className="p-10 text-center"><ReceiptText className="mx-auto text-zinc-400" size={30} /><p className="mt-3 font-semibold">{filtered?"Filtrelerle eşleşen kayıt yok":"Açık bakiye bulunmuyor"}</p><p className="mt-1 text-sm text-zinc-500">{filtered?"Arama veya borç filtrelerini değiştirin.":"Öğrencilerin açık ya da gecikmiş borcu bulunmuyor."}</p></div>; }
