import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";

const sourceRoot = path.resolve("src");
const files = [];
const visit = (directory) => {
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    const target = path.join(directory, entry.name);
    if (entry.isDirectory()) visit(target);
    else if (entry.name.endsWith(".tsx")) files.push(target);
  }
};
visit(sourceRoot);

for (const file of files) {
  const source = fs.readFileSync(file, "utf8");
  for (const match of source.matchAll(/<Button\b(?=[^>]*\bform=)[^>]*>/g)) {
    assert.match(match[0], /\btype="submit"/, `${path.relative(sourceRoot, file)} form düğmesinde type="submit" eksik: ${match[0]}`);
  }
}

const attendance = fs.readFileSync(path.join(sourceRoot, "components/operations/attendance-section.tsx"), "utf8");
for (const expected of [
  "Tümünü Geldi Yap",
  "Seçimleri temizle",
  "Son kaydı geri yükle",
  "Yoklamayı Kaydet",
  "Kaydediliyor...",
  "aria-busy={save.isPending}",
  "saveLock.current",
  "setSaveError(message)",
  "sticky bottom-0",
  "operationsApi.saveAttendance",
]) assert.ok(attendance.includes(expected), `Yoklama davranışı eksik: ${expected}`);

const roomDialog = fs.readFileSync(path.join(sourceRoot, "components/classes/room-management-dialog.tsx"), "utf8");
for (const expected of ["type=\"submit\" form=\"room-form\"", "aria-busy={save.isPending}", "saveLock.current", "setErrors(next)", "invalidateQueries", "Stüdyo başarıyla güncellendi."]) {
  assert.ok(roomDialog.includes(expected), `Stüdyo güncelleme davranışı eksik: ${expected}`);
}

const api = fs.readFileSync(path.join(sourceRoot, "lib/api.ts"), "utf8");
for (const status of ["status === 401", "status === 403", "status === 404", "status === 409", "status >= 500", "AbortError", "Sunucuya ulaşılamadı"]) {
  assert.ok(api.includes(status), `Ortak API hata eşlemesi eksik: ${status}`);
}

console.log(`operational action tests passed (${files.length} TSX files checked)`);
