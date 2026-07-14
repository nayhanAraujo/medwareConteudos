<template>
  <aside class="w-64 flex-shrink-0 bg-gradient-to-b from-gray-900 to-gray-800 text-gray-200 flex flex-col">
    <div class="px-6 py-5 text-center border-b border-white/10">
      <h5 class="text-lg font-semibold font-manrope text-white tracking-wide m-0">MDW - SGC</h5>
    </div>
    <nav class="flex-1 overflow-y-auto py-2">
      <NuxtLink
        v-for="item in menu"
        :key="item.path"
        :to="item.path"
        class="flex items-center px-6 py-3.5 text-sm font-medium no-underline transition-all border-l-4"
        :class="
          isMenuActive(route.path, item)
            ? 'bg-white/10 text-white border-white'
            : 'text-gray-300 border-transparent hover:bg-white/5 hover:text-white hover:border-gray-500'
        "
      >
        <i :class="`bi bi-${item.icon} text-lg w-6 mr-3 text-center`" />
        <span>{{ item.label }}</span>
      </NuxtLink>
    </nav>
    <div class="border-t border-white/10">
      <a
        href="#"
        class="flex items-center px-6 py-3.5 text-sm font-medium no-underline text-gray-300 border-l-4 border-transparent hover:bg-white/5 hover:text-rose-300 hover:border-rose-400 transition-all"
        @click.prevent="onLogout"
      >
        <i class="bi bi-box-arrow-left text-lg w-6 mr-3 text-center" />
        <span>Sair</span>
      </a>
    </div>
  </aside>
</template>

<script setup lang="ts">
const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const swal = useSwal()
const menu = useAppMenu()

async function onLogout() {
  const r = await swal.confirm('Deseja sair do sistema?', 'Logout')
  if (r.isConfirmed) {
    auth.logout()
    await router.push('/login')
  }
}
</script>
