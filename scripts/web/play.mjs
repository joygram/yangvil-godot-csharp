// 플레이 실측: 같은 흐름(마을 → 모험 → 출발 → 자동 전투 → 결과)을 같은 좌표로 진행하며 프레임 측정.
// 진행 = 게임 콘솔 신호 "[screen] <screen_id>" (App.Go) 만 기다림. 고정 시간 대기 없음.
// 잼: 마을 = rAF 프레임 600개 표본, 전투 = battle 신호 ~ result 신호 전체. FPS·느린 프레임(>33ms)·최장 프레임·메인 스레드 점유
// 사용: node scripts/web/play.mjs [회수]   ONLY=<판>, SHOT=1 → Builds/play-<판>-<단계>.png
import { mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { chromium } from "playwright-core";

const RUNS = Number(process.argv[2] || 1);
const SITES = [
  { name: "Unity", url: "https://yangvil.deuklab.workers.dev/" },
  { name: "Godot", url: "https://yangvil-godot-csharp.deuklab.workers.dev/" },
  { name: "GodotAOT", url: "https://yangvil-godot-csharp-aot.deuklab.workers.dev/" },
].filter((s) => !process.env.ONLY || s.name === process.env.ONLY);
// 540x960 화면 좌표 (두 판 배치 같음)
const ADVENTURE = { x: 453, y: 895 };
const DEPART = { x: 270, y: 850 };
const AUTO = { x: 190, y: 925 };
const LIMIT = 300_000; // 신호가 끝내 안 오면 실패로 끊음

// rAF 간격 기록 시작/끝 (페이지 안)
const startFrames = () => {
  window.__gaps = [];
  let last = performance.now();
  const tick = (t) => { window.__gaps.push(t - last); last = t; if (!window.__stop) requestAnimationFrame(tick); };
  window.__stop = false;
  requestAnimationFrame(tick);
};
const stopFrames = () => { window.__stop = true; return window.__gaps; };
const sampleFrames = (count) => new Promise((done) => {
  const gaps = [];
  let last = performance.now();
  const tick = (t) => { gaps.push(t - last); last = t; if (gaps.length < count) requestAnimationFrame(tick); else done(gaps); };
  requestAnimationFrame(tick);
});

function stat(r) {
  const secs = r.gaps.reduce((a, b) => a + b, 0) / 1000;
  const slow = r.gaps.filter((g) => g > 33.4).length;
  return `${(r.gaps.length / secs).toFixed(0)}fps, 느린 ${((slow / r.gaps.length) * 100).toFixed(1)}%, 최장 ${Math.max(...r.gaps).toFixed(0)}ms, 메인 ${((r.task / secs) * 100).toFixed(0)}% (${secs.toFixed(1)}s)`;
}

async function play(site, run) {
  const profile = mkdtempSync(join(tmpdir(), "yv-play-"));
  const context = await chromium.launchPersistentContext(profile, {
    channel: "chrome", headless: true, viewport: { width: 540, height: 960 },
    args: ["--enable-unsafe-swiftshader", "--disable-frame-rate-limit", "--disable-gpu-vsync"],
  });
  const shot = async (page, step) => { if (process.env.SHOT) await page.screenshot({ path: `Builds/play-${site.name}-${step}.png` }); };
  try {
    const page = context.pages()[0] ?? (await context.newPage());
    const cdp = await context.newCDPSession(page);
    await cdp.send("Performance.enable");
    const task = async () => (await cdp.send("Performance.getMetrics")).metrics.find((m) => m.name === "TaskDuration").value;
    const screen = (id) => page.waitForEvent("console", { predicate: (m) => m.text().includes(`[screen] ${id}`), timeout: LIMIT });

    const village = screen("village");
    await page.goto(site.url, { waitUntil: "commit" });
    await village;
    await shot(page, "1-village");
    let t0 = await task();
    const vg = await page.evaluate(sampleFrames, 600);
    const v = { gaps: vg, task: (await task()) - t0 };

    const map = screen("stage_select");
    await page.mouse.click(ADVENTURE.x, ADVENTURE.y);
    await map;
    await shot(page, "2-map");
    const battle = screen("battle");
    await page.mouse.click(DEPART.x, DEPART.y);
    await battle;
    const result = screen("result");
    await page.evaluate(startFrames);
    t0 = await task();
    await page.mouse.click(AUTO.x, AUTO.y);
    await shot(page, "3-battle");
    await result;
    const b = { gaps: await page.evaluate(stopFrames), task: (await task()) - t0 };
    await shot(page, "4-result");
    console.log(`[play] ${site.name} #${run}: 마을 ${stat(v)} | 전투 ${stat(b)}`);
  } finally {
    await context.close();
    rmSync(profile, { recursive: true, force: true });
  }
}

for (const site of SITES) for (let i = 1; i <= RUNS; i++) {
  try { await play(site, i); } catch (e) { console.log(`[play] ${site.name} #${i}: ✘ ${e.message.split("\n")[0]}`); }
}
