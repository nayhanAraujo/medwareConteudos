// https://nuxt.com/docs/api/configuration/nuxt-config
import tailwindcss from '@tailwindcss/vite'

export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',
  devtools: { enabled: true },
  modules: ['@pinia/nuxt'],
  css: [
    '~/assets/css/tailwind.css',
    '~/assets/css/app-layout.css',
    '~/assets/css/scripts.css',
    '~/assets/css/studio-swal.css'
  ],
  vite: {
    plugins: [tailwindcss()],
    server: {
      proxy: {
        '/static': {
          target: 'http://localhost:5080',
          changeOrigin: true
        }
      }
    }
  },
  runtimeConfig: {
    apiServerBase: process.env.NUXT_API_SERVER_BASE || process.env.DOTNET_API_BASE || 'http://localhost:5080',
    docsApiBase: process.env.NUXT_DOCS_API_BASE || process.env.NUXT_API_SERVER_BASE || 'http://localhost:5080',
    docsSandboxBase: process.env.NUXT_DOCS_SANDBOX_BASE || 'http://localhost:5081',
    docsPublicBase: process.env.NUXT_DOCS_PUBLIC_BASE || 'http://localhost:5080',
    docsSandboxPublicBase: process.env.NUXT_DOCS_SANDBOX_PUBLIC_BASE || 'http://localhost:5081',
    docsSandboxInstanceId: process.env.NUXT_DOCS_SANDBOX_INSTANCE_ID || '',
    docsSupportUrl: process.env.NUXT_DOCS_SUPPORT_URL || '',
    public: {
      apiBase: process.env.NUXT_PUBLIC_API_BASE || '/api-dotnet'
    }
  },
  nitro: {
    publicAssets: [
      {
        dir: '../../static',
        baseURL: '/static'
      },
      {
        dir: '../../../uploads',
        baseURL: '/uploads'
      }
    ],
    // Em `nuxt dev` o Vite não usa nitro.publicAssets — proxy /static → API.
    // Em produção, publicAssets acima cobre a pasta static/ do repositório.
    devProxy: {
      '/api-dotnet': {
        target: 'http://localhost:5080',
        changeOrigin: true,
        prependPath: false,
        rewrite: (path: string) => path.replace(/^\/api-dotnet/, '')
      },
      '/static': {
        target: 'http://localhost:5080/static',
        changeOrigin: true
      }
    }
  },
  app: {
    head: {
      title: 'MDW - SGC',
      link: [
        { rel: 'stylesheet', href: 'https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.0/font/bootstrap-icons.css' },
        { rel: 'preconnect', href: 'https://fonts.googleapis.com' },
        { rel: 'preconnect', href: 'https://fonts.gstatic.com', crossorigin: '' },
        {
          rel: 'stylesheet',
          href: 'https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&family=Manrope:wght@500;600;700&display=swap'
        }
      ]
    }
  }
})
