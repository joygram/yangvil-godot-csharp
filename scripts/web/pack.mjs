// 웹 빌드 압축: .wasm·.pck → .br (brotli 11), 원본은 .assetsignore 로 업로드 제외. 25MiB 넘는 파일 있으면 실패.
import { brotliCompressSync, constants } from "node:zlib";
import { readdirSync, readFileSync, statSync, writeFileSync } from "node:fs";
import { join } from "node:path";

const OUT = process.argv[2];
const LIMIT = 25 * 1024 * 1024;
const ignore = [];
for (const name of readdirSync(OUT).sort()) {
  if (!/\.(wasm|pck)$/.test(name)) continue;
  const data = readFileSync(join(OUT, name));
  const br = brotliCompressSync(data, { params: { [constants.BROTLI_PARAM_QUALITY]: 11, [constants.BROTLI_PARAM_SIZE_HINT]: data.length } });
  writeFileSync(join(OUT, name + ".br"), br);
  ignore.push(name);
  console.log(`[pack] ${name} ${(data.length / 1048576).toFixed(1)}MiB → .br ${(br.length / 1048576).toFixed(1)}MiB`);
}
writeFileSync(join(OUT, ".assetsignore"), [...ignore, ".assetsignore", ""].join("\n"));
const big = readdirSync(OUT).filter((n) => !ignore.includes(n) && statSync(join(OUT, n)).isFile() && statSync(join(OUT, n)).size > LIMIT);
if (big.length) {
  console.error(`[pack] ✘ 25MiB 초과: ${big.join(", ")}`);
  process.exit(1);
}
