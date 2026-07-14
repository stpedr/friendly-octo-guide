// TOTP RFC 6238 no cliente (Web Crypto, HMAC-SHA1, passo 30s, janela ±1) — o
// desbloqueio local do dashboard de observabilidade. Puro; sem dependência externa.
// É verificação LOCAL de posse do fator, não emissão de sessão — o Gateway ainda
// exige seu próprio login pra qualquer dado que saia da rede.
(function (global) {
  function base32Decode(input) {
    const alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    const clean = input.replace(/=+$/g, "").toUpperCase().replace(/\s/g, "");
    let bits = 0;
    let value = 0;
    const out = [];
    for (const ch of clean) {
      const idx = alphabet.indexOf(ch);
      if (idx === -1) throw new Error("seed Base32 inválido");
      value = (value << 5) | idx;
      bits += 5;
      if (bits >= 8) {
        bits -= 8;
        out.push((value >> bits) & 0xff);
      }
    }
    return new Uint8Array(out);
  }

  async function hotp(keyBytes, counter) {
    const buf = new ArrayBuffer(8);
    const view = new DataView(buf);
    // counter em 64 bits big-endian (high word cabe em 32 bits pro nosso range).
    view.setUint32(4, counter >>> 0, false);
    view.setUint32(0, Math.floor(counter / 2 ** 32), false);

    const key = await crypto.subtle.importKey(
      "raw", keyBytes, { name: "HMAC", hash: "SHA-1" }, false, ["sign"]);
    const mac = new Uint8Array(await crypto.subtle.sign("HMAC", key, buf));

    const offset = mac[mac.length - 1] & 0x0f;
    const bin =
      ((mac[offset] & 0x7f) << 24) |
      (mac[offset + 1] << 16) |
      (mac[offset + 2] << 8) |
      mac[offset + 3];
    return (bin % 1_000_000).toString().padStart(6, "0");
  }

  // Verdadeiro se o código bate em algum passo da janela ±1 (tolerância de relógio).
  async function verify(seedBase32, code, now = Date.now()) {
    if (!/^[0-9]{6}$/.test(code)) return false;
    const key = base32Decode(seedBase32);
    const step = Math.floor(now / 1000 / 30);
    for (const c of [step - 1, step, step + 1]) {
      if (await hotp(key, c) === code) return true;
    }
    return false;
  }

  global.TOTP = { verify, base32Decode };
})(window);
