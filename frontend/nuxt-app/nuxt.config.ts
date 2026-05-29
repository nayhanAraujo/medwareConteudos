// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',
  devtools: { enabled: true },
  modules: ['@pinia/nuxt'],
  css: ['bootstrap/dist/css/bootstrap.min.css', '~/assets/css/app-layout.css', '~/assets/css/scripts.css'],
  runtimeConfig: {
    public: {
      apiBase: '/api-dotnet'
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
    devProxy: {
      '/api-dotnet': {
        target: 'http://localhost:5080',
        changeOrigin: true,
        prependPath: false,
        rewrite: (path: string) => path.replace(/^\/api-dotnet/, '')
      },
      '/static': {
        target: 'http://localhost:5080',
        changeOrigin: true
      }
    }
  },
  app: {
    head: {
      title: 'MDW - SGC',
      link: [
        { rel: 'stylesheet', href: 'https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.0/font/bootstrap-icons.css' }
      ]
    }
  }
})
