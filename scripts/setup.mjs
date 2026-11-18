// .NET SDK 확인: game/global.json 의 메이저가 없으면 설치 안내 (Windows = winget 설치).
import { spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const ROOT = join(dirname(fileURLToPath(import.meta.url)), "..");
const major = JSON.parse(readFileSync(join(ROOT, "game/global.json"), "utf8")).sdk.version.split(".")[0];
const WIN = process.platform === "win32";

const has = () => {
  const r = spawnSync("dotnet", ["--list-sdks"], { encoding: "utf8", shell: WIN });
  return r.status === 0 && r.stdout.split(/\r?\n/).some((l) => l.startsWith(`${major}.`));
};

if (has()) process.exit(0);
if (!WIN) {
  console.error(`[setup] .NET SDK ${major} 없음 → https://dotnet.microsoft.com/download/dotnet/${major}.0`);
  process.exit(1);
}
console.log(`[setup] .NET SDK ${major} 설치 (winget)`);
spawnSync("winget", ["install", "--id", `Microsoft.DotNet.SDK.${major}`, "-e", "--accept-source-agreements", "--accept-package-agreements"], { stdio: "inherit", shell: true });
if (!has()) {
  console.error(`[setup] SDK ${major} 안 보임 → 새 터미널에서 다시 실행`);
  process.exit(1);
}
