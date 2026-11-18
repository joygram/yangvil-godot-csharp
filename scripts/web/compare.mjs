// Unity 판 ↔ Godot C# 판 웹 비교: 같은 PC·설치된 Chrome(headless)·캐시 없음·새 프로필, 판마다 RUNS 회.
// 잼: 전송 바이트(Network), 첫 화면(로딩 덮개 사라질 때까지), 메모리(그 Chrome 프로세스 합계, 로딩 5초 뒤)
// 사용: node scripts/web/compare.mjs [회수]
import { spawnSync } from "node:child_process";
import { mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { chromium } from "playwright-core";

const RUNS = Number(process.argv[2] || 3);
const SITES = [
  { name: "Unity WebGL", url: "https://yangvil.deuklab.workers.dev/", overlay: "loading" },
  { name: "godot-enhanced C#", url: "https://yangvil-godot-csharp.deuklab.workers.dev/", overlay: "status" },
];
// 첫 화면 = 로딩 덮개가 한 번 보였다가 사라진 때.
const shown = (id) => { const e = document.getElementById(id); const s = e && getComputedStyle(e); return !!s && s.display !== "none" && s.visibility !== "hidden"; };
const hidden = (id) => { const e = document.getElementById(id); if (!e || !e.isConnected) return true; const s = getComputedStyle(e); return s.display === "none" || s.visibility === "hidden"; };

// 이 프로필로 뜬 Chrome 프로세스들의 전용 메모리 합계 (Windows).
function memory(profile) {
  if (process.platform !== "win32") return null;
  const ps = `(Get-CimInstance Win32_Process -Filter "Name='chrome.exe'" | Where-Object { $_.CommandLine -like '*${profile}*' } | ForEach-Object { (Get-Process -Id $_.ProcessId -ErrorAction SilentlyContinue).PrivateMemorySize64 } | Measure-Object -Sum).Sum`;
  return Number(spawnSync("powershell", ["-NoProfile", "-Command", ps], { encoding: "utf8" }).stdout.trim()) || null;
}

async function measure(site) {
  const profile = mkdtempSync(join(tmpdir(), "yv-cmp-"));
  const context = await chromium.launchPersistentContext(profile, { channel: "chrome", headless: true, viewport: { width: 540, height: 960 }, args: ["--enable-unsafe-swiftshader"] });
  try {
    const page = context.pages()[0] ?? (await context.newPage());
    const cdp = await context.newCDPSession(page);
    let bytes = 0;
    cdp.on("Network.loadingFinished", (e) => { bytes += e.encodedDataLength; });
    await cdp.send("Network.enable");
    await cdp.send("Network.setCacheDisabled", { cacheDisabled: true });
    const errors = [];
    page.on("pageerror", (e) => errors.push(e.message));
    const t0 = performance.now();
    await page.goto(site.url, { waitUntil: "commit" });
    let ready = null;
    try {
      await page.waitForFunction(shown, site.overlay, { timeout: 120_000, polling: 10 });
      await page.waitForFunction(hidden, site.overlay, { timeout: 120_000, polling: 50 });
      ready = performance.now() - t0;
    } catch { /* 시간 초과 */ }
    await page.waitForTimeout(5000);
    if (process.env.SHOT) await page.screenshot({ path: `Builds/compare-${site.name.split(" ")[0]}.png` });
    return { ready, bytes, mem: memory(profile), errors: errors.length };
  } finally {
    await context.close();
    rmSync(profile, { recursive: true, force: true });
  }
}

const mb = (n) => (n / 1048576).toFixed(1);
const median = (a) => [...a].sort((x, y) => x - y)[Math.floor(a.length / 2)];
for (const site of SITES) {
  const runs = [];
  for (let i = 0; i < RUNS; i++) {
    const r = await measure(site);
    runs.push(r);
    console.log(`[compare] ${site.name} #${i + 1}: 첫 화면 ${r.ready ? (r.ready / 1000).toFixed(2) + "s" : "시간 초과"}, 전송 ${mb(r.bytes)}MB, 메모리 ${r.mem ? mb(r.mem) + "MB" : "-"}, 오류 ${r.errors}`);
  }
  const ok = runs.filter((r) => r.ready);
  console.log(`[compare] ${site.name} 중앙값: 첫 화면 ${ok.length ? (median(ok.map((r) => r.ready)) / 1000).toFixed(2) + "s" : "-"}, 전송 ${mb(median(runs.map((r) => r.bytes)))}MB, 메모리 ${runs[0].mem ? mb(median(runs.map((r) => r.mem))) + "MB" : "-"}`);
}
