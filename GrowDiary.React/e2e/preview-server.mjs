// Minimal static server that mirrors how the ASP.NET backend serves the SPA:
// it serves the real production build from GrowDiary.Web/wwwroot, and for any
// non-file (SPA) route it returns index.html with a <base href="/"> injected right
// after <head> — exactly like Program.cs's MapFallback + InjectBaseHref. This makes
// the e2e smoke suite exercise the same bundle + base-href behavior as production,
// so deep links like /grows/1 resolve their relative assets correctly.
import { createServer } from 'node:http'
import { readFile, stat } from 'node:fs/promises'
import { extname, join, normalize } from 'node:path'
import { fileURLToPath } from 'node:url'

const PORT = Number(process.argv[2] ?? 4173)
const API_PROXY = process.env.GROW_OS_API_PROXY ?? null
const ROOT = fileURLToPath(new URL('../../GrowDiary.Web/wwwroot', import.meta.url))

const TYPES = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.ico': 'image/x-icon',
  '.woff2': 'font/woff2',
  '.woff': 'font/woff',
  '.webmanifest': 'application/manifest+json',
}

function injectBaseHref(html) {
  const tag = '<base href="/" />'
  const i = html.toLowerCase().indexOf('<head>')
  if (i < 0) return html
  const at = i + '<head>'.length
  return html.slice(0, at) + tag + html.slice(at)
}

async function serveIndex(res) {
  const html = await readFile(join(ROOT, 'index.html'), 'utf8')
  res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8', 'Cache-Control': 'no-store' })
  res.end(injectBaseHref(html))
}

const server = createServer(async (req, res) => {
  try {
    const url = new URL(req.url, `http://localhost:${PORT}`)
    if (url.pathname.startsWith('/api/')) {
      // Opt-in: point at a running backend to eyeball real data through the same
      // <base href> the add-on serves. Without it, behave like a down upstream (502),
      // which is what the backend-less smoke suite expects.
      if (API_PROXY) {
        const upstream = await fetch(new URL(req.url, API_PROXY), {
          method: req.method,
          headers: { ...req.headers, host: new URL(API_PROXY).host },
          body: ['GET', 'HEAD'].includes(req.method ?? 'GET') ? undefined : req,
          duplex: 'half',
        }).catch(() => null)
        if (!upstream) {
          res.writeHead(502).end('upstream unreachable')
          return
        }
        res.writeHead(upstream.status, { 'Content-Type': upstream.headers.get('content-type') ?? 'application/json' })
        res.end(Buffer.from(await upstream.arrayBuffer()))
        return
      }
      res.writeHead(502).end('backend not running under smoke test')
      return
    }
    const rel = normalize(decodeURIComponent(url.pathname)).replace(/^(\.\.[/\\])+/, '')
    const filePath = join(ROOT, rel)
    if (!filePath.startsWith(ROOT)) {
      res.writeHead(403).end('forbidden')
      return
    }
    const info = await stat(filePath).catch(() => null)
    if (info?.isFile()) {
      const body = await readFile(filePath)
      res.writeHead(200, { 'Content-Type': TYPES[extname(filePath)] ?? 'application/octet-stream' })
      res.end(body)
      return
    }
    // SPA fallback for everything else.
    await serveIndex(res)
  } catch (err) {
    res.writeHead(500).end(String(err))
  }
})

server.listen(PORT, () => console.log(`smoke preview server on http://localhost:${PORT} (root: ${ROOT})`))
