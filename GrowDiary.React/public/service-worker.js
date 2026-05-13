const CACHE_PREFIX = 'grow-os-'
const CACHE_NAME = 'grow-os-app-shell-v0.1.1'

const APP_SHELL_URLS = [
  '/',
  '/action',
  '/live',
  '/manifest.webmanifest',
  '/icons/icon-192.png',
  '/icons/icon-512.png',
  '/icons/maskable-icon-512.png',
  '/icons/apple-touch-icon.png',
  '/offline.html',
]

const NETWORK_ONLY_PREFIXES = ['/api', '/uploads', '/App_Data', '/app_data']

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => cache.addAll(APP_SHELL_URLS)),
  )
  self.skipWaiting()
})

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((cacheNames) =>
        Promise.all(
          cacheNames
            .filter((cacheName) => cacheName.startsWith(CACHE_PREFIX) && cacheName !== CACHE_NAME)
            .map((cacheName) => caches.delete(cacheName)),
        ),
      )
      .then(() => self.clients.claim()),
  )
})

self.addEventListener('fetch', (event) => {
  const request = event.request

  if (request.method !== 'GET') {
    return
  }

  const url = new URL(request.url)
  if (url.origin !== self.location.origin) {
    return
  }

  if (isNetworkOnlyPath(url.pathname)) {
    event.respondWith(fetch(request))
    return
  }

  if (url.pathname.startsWith('/assets/') || url.pathname.startsWith('/icons/')) {
    event.respondWith(cacheFirst(request))
    return
  }

  if (request.mode === 'navigate') {
    event.respondWith(navigationNetworkFirst(request))
    return
  }

  event.respondWith(networkFirst(request))
})

function isNetworkOnlyPath(pathname) {
  return NETWORK_ONLY_PREFIXES.some((prefix) => pathname.startsWith(prefix))
}

async function cacheFirst(request) {
  const cached = await caches.match(request)
  if (cached) {
    return cached
  }

  const response = await fetch(request)
  await cacheIfOk(request, response)
  return response
}

async function navigationNetworkFirst(request) {
  try {
    const response = await fetch(request)
    await cacheIfOk(request, response)
    return response
  } catch {
    return (
      (await caches.match('/offline.html')) ||
      (await caches.match('/')) ||
      Response.error()
    )
  }
}

async function networkFirst(request) {
  try {
    const response = await fetch(request)
    await cacheIfOk(request, response)
    return response
  } catch {
    return (await caches.match(request)) || Response.error()
  }
}

async function cacheIfOk(request, response) {
  if (!response || !response.ok) {
    return
  }

  const cache = await caches.open(CACHE_NAME)
  await cache.put(request, response.clone())
}
