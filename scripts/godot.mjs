// godot-enhanced 최신 C# 키트로 Godot 실행. 인자는 그대로 넘김.
// - GODOT_BIN 있으면 그것 사용
// - 아니면 GitHub 릴리스 dotnet-web-* 중 최신 (이 OS·CPU 키트) → ~/.godot-enhanced/<태그>-<자산id> 에 설치 (저장소끼리 공유)
// - 설치 시 NuGet 소스 "godot-enhanced" = 키트 nuget 폴더로 등록
// - 최신 확인 1시간 캐시, 오프라인이면 설치된 가장 새 키트. 고정: GODOT_ENHANCED_TAG
import { spawnSync } from "node:child_process";
import { chmodSync, copyFileSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from "node:fs";
import { homedir, tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const ROOT = join(dirname(fileURLToPath(import.meta.url)), "..");
const REPO = "joygram/godot-enhanced";
const HOME = process.env.GODOT_ENHANCED_HOME || join(homedir(), ".godot-enhanced");
const WIN = process.platform === "win32";
const ARCH = process.arch === "arm64" ? "arm64" : "x86_64";
const KIT = { win32: `windows-${ARCH}`, linux: `linux-${ARCH}`, darwin: "macos-arm64" }[process.platform];
const EXE = WIN ? /^godot\.windows\.editor\.\w+\.mono\.console\.exe$/
  : process.platform === "darwin" ? /^Godot$/ : /^godot\.linuxbsd\.editor\.\w+\.mono$/;
const SUFFIX = `.dotnet-web.${KIT}.zip`;
const log = (m) => console.error(`[godot] ${m}`);

function run(cmd, args) {
  const r = spawnSync(cmd, args, { stdio: "inherit", shell: WIN, cwd: tmpdir() });
  if (r.status !== 0) throw new Error(`${cmd} ${args.join(" ")} 실패 (${r.status})`);
}

function find(dir, test) {
  if (!existsSync(dir)) return null;
  for (const e of readdirSync(dir, { withFileTypes: true })) {
    const p = join(dir, e.name);
    if (e.isFile() && test(e.name)) return p;
    if (e.isDirectory() && e.name !== "GodotSharp") {
      const hit = find(p, test);
      if (hit) return hit;
    }
  }
  return null;
}

async function gh(path) {
  const token = process.env.GH_TOKEN || process.env.GITHUB_TOKEN;
  const r = await fetch(`https://api.github.com/repos/${REPO}/${path}`, {
    headers: { Accept: "application/vnd.github+json", ...(token ? { Authorization: `Bearer ${token}` } : {}) },
  });
  if (!r.ok) throw new Error(`GitHub ${r.status}: ${path}`);
  return r.json();
}

function installed() {
  if (!existsSync(HOME)) return [];
  return readdirSync(HOME).filter((n) => n.startsWith("dotnet-web-") && existsSync(join(HOME, n, ".installed"))).sort();
}

// 최신 = { tag, assetId }
async function latest() {
  if (process.env.GODOT_ENHANCED_TAG) return { tag: process.env.GODOT_ENHANCED_TAG, assetId: null };
  const cache = join(HOME, ".latest-dotnet.json");
  if (existsSync(cache) && Date.now() - statSync(cache).mtimeMs < 3600_000) return JSON.parse(readFileSync(cache, "utf8"));
  try {
    const rel = (await gh("releases?per_page=50"))
      .filter((r) => !r.draft && r.tag_name?.startsWith("dotnet-web-") && r.assets.some((a) => a.name.endsWith(SUFFIX)))
      .sort((a, b) => b.published_at.localeCompare(a.published_at))[0];
    if (!rel) throw new Error(`${KIT} C# 키트 릴리스 없음`);
    const found = { tag: rel.tag_name, assetId: rel.assets.find((a) => a.name.endsWith(SUFFIX)).id };
    mkdirSync(HOME, { recursive: true });
    writeFileSync(cache, JSON.stringify(found));
    return found;
  } catch (e) {
    const last = installed().at(-1);
    if (!last) throw e;
    log(`최신 확인 실패(${e.message}) → 설치된 ${last}`);
    return { tag: last, assetId: null };
  }
}

async function install({ tag, assetId }) {
  const ready = assetId ? `${tag}-${assetId}` : installed().filter((n) => n.startsWith(tag)).at(-1);
  if (ready) {
    const exe = find(join(HOME, ready), (n) => EXE.test(n));
    if (exe && existsSync(join(HOME, ready, ".installed"))) return exe;
  }
  const rel = await gh(`releases/tags/${tag}`);
  const asset = rel.assets.find((a) => a.name.endsWith(SUFFIX));
  if (!asset) throw new Error(`${tag} 에 ${KIT} 키트 없음`);
  const dir = join(HOME, `${tag}-${asset.id}`);
  log(`godot-enhanced ${tag} (${KIT}) 받는 중 → ${dir}`);
  const res = await fetch(asset.browser_download_url);
  if (!res.ok) throw new Error(`받기 실패 ${res.status}`);
  const zip = join(tmpdir(), asset.name);
  writeFileSync(zip, Buffer.from(await res.arrayBuffer()));
  rmSync(dir, { recursive: true, force: true });
  mkdirSync(dir, { recursive: true });
  if (process.platform === "linux") run("unzip", ["-q", zip, "-d", dir]);
  else if (process.platform === "darwin") run("ditto", ["-x", "-k", zip, dir]);
  else run("tar", ["-xf", zip, "-C", dir]);
  rmSync(zip, { force: true });

  const nuget = find(dir, (n) => n.startsWith("Godot.NET.Sdk.") && n.endsWith(".nupkg"));
  if (!nuget) throw new Error(`${dir} 에 Godot.NET.Sdk 패키지 없음`);
  spawnSync("dotnet", ["nuget", "remove", "source", "godot-enhanced"], { shell: WIN, cwd: tmpdir() });
  run("dotnet", ["nuget", "add", "source", dirname(nuget), "--name", "godot-enhanced"]);
  const version = tag.replace(/^dotnet-web-/, "").split("-")[0];
  for (const pkg of ["godot.net.sdk", "godotsharp", "godotsharpeditor", "godot.sourcegenerators"])
    rmSync(join(process.env.NUGET_PACKAGES || join(homedir(), ".nuget", "packages"), pkg, version), { recursive: true, force: true });

  const exe = find(dir, (n) => EXE.test(n));
  if (!exe) throw new Error(`${dir} 에 편집기 실행 파일 없음`);
  if (!WIN) chmodSync(exe, 0o755);
  writeFileSync(join(dir, ".installed"), JSON.stringify({ tag, assetId: asset.id }));
  log(`설치 완료: ${tag}`);
  return exe;
}

// 웹 내보내기 템플릿: 키트의 web_*.zip → export_templates/<godot>.stable.mono.enhanced-<버전> (없을 때만)
function templates(exe) {
  const kit = dirname(exe);
  const web = find(kit, (n) => n === "web_release.zip");
  const m = /dotnet-web-([\d.]+)-([\d.]+)/.exec(kit);
  if (!web || !m) return;
  const base = WIN ? join(process.env.APPDATA, "Godot")
    : process.platform === "darwin" ? join(homedir(), "Library", "Application Support", "Godot")
    : join(process.env.XDG_DATA_HOME || join(homedir(), ".local", "share"), "godot");
  const target = join(base, "export_templates", `${m[1]}.stable.mono.enhanced-${m[2]}`);
  mkdirSync(target, { recursive: true });
  for (const f of readdirSync(dirname(web)).filter((n) => /^web_.*\.zip$/.test(n)))
    if (!existsSync(join(target, f))) copyFileSync(join(dirname(web), f), join(target, f));
}

let exe;
try {
  exe = process.env.GODOT_BIN || (KIT ? await install(await latest()) : null);
  if (!exe) throw new Error(`지원 안 하는 OS: ${process.platform}`);
  if (!process.env.GODOT_BIN) templates(exe);
} catch (e) {
  log(`✘ ${e.message}`);
  process.exit(1);
}
const r = spawnSync(exe, process.argv.slice(2), { stdio: "inherit", cwd: ROOT });
process.exit(r.status ?? 1);
