<template>
  <aside class="sidebar">
    <div class="sidebar-header">
      <h5 class="app-title">MDW - SGC</h5>
    </div>
    <nav class="sidebar-menu">
      <NuxtLink
        v-for="item in menu"
        :key="item.path"
        :to="item.path"
        class="sidebar-link"
        :class="{ active: isMenuActive(route.path, item) }"
      >
        <i :class="`bi bi-${item.icon} link-icon`" />
        <span class="link-text">{{ item.label }}</span>
      </NuxtLink>
    </nav>
    <div class="sidebar-footer">
      <a href="#" class="sidebar-link logout-link" @click.prevent="onLogout">
        <i class="bi bi-box-arrow-left link-icon" />
        <span class="link-text">Sair</span>
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
