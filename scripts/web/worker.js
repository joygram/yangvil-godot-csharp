// 냥빌 Godot C# 웹 워커: 스레드판 요구 헤더(COOP/COEP) + .wasm·.pck 는 미리 압축한 .br 로 전달.
const TYPES = { ".wasm": "application/wasm", ".pck": "application/octet-stream" };

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    const ext = Object.keys(TYPES).find((e) => url.pathname.endsWith(e));
    let res;
    if (ext) {
      const br = await env.ASSETS.fetch(new Request(new URL(url.pathname + ".br", url), request));
      if (br.ok) {
        const headers = new Headers(br.headers);
        headers.set("Content-Type", TYPES[ext]);
        headers.set("Content-Encoding", "br");
        res = new Response(br.body, { status: 200, headers, encodeBody: "manual" });
      }
    }
    res ??= await env.ASSETS.fetch(request);
    const out = new Response(res.body, res);
    out.headers.set("Cross-Origin-Opener-Policy", "same-origin");
    out.headers.set("Cross-Origin-Embedder-Policy", "require-corp");
    out.headers.set("Cross-Origin-Resource-Policy", "same-origin");
    return out;
  },
};
