import assert from "node:assert/strict";
import fs from "node:fs";
import vm from "node:vm";
import ts from "typescript";

const source = fs.readFileSync(new URL("../src/lib/schedule-days.ts", import.meta.url), "utf8");
const compiled = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
const cjsModule = { exports: {} };
const logged = [];
vm.runInNewContext(compiled, { module: cjsModule, exports: cjsModule.exports, console: { error: (...args) => logged.push(args) } });
const { normalizeScheduleDay, scheduleDayLabel, scheduleDayTimeText } = cjsModule.exports;

assert.equal(scheduleDayLabel(3), "Çarşamba");
assert.equal(scheduleDayLabel(4), "Perşembe");
assert.equal(normalizeScheduleDay("Wednesday"), 3);
assert.equal(normalizeScheduleDay("Thursday"), 4);
assert.equal(scheduleDayTimeText(3, "19:00:00", "20:15:00"), "Çarşamba 19:00–20:15");
assert.equal(scheduleDayTimeText("Thursday", "20:15:00", "21:30:00"), "Perşembe 20:15–21:30");
assert.equal(scheduleDayLabel("broken"), "Bilinmeyen gün");
assert.equal(logged.length, 1);

console.log("schedule day mapping tests passed");
