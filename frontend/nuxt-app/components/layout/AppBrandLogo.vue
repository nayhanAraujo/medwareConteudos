<template>
  <div class="flex flex-col items-center gap-2">
    <svg viewBox="0 0 64 64" class="h-12 w-12 drop-shadow-[0_6px_16px_rgba(96,165,250,0.45)]" aria-hidden="true">
      <defs>
        <linearGradient id="mdw-logo-bg" x1="8" y1="4" x2="56" y2="60" gradientUnits="userSpaceOnUse">
          <stop stop-color="#60a5fa" />
          <stop offset="0.55" stop-color="#2563eb" />
          <stop offset="1" stop-color="#7c3aed" />
        </linearGradient>
        <linearGradient id="mdw-logo-shine" x1="16" y1="6" x2="48" y2="32" gradientUnits="userSpaceOnUse">
          <stop stop-color="#ffffff" stop-opacity="0.55" />
          <stop offset="1" stop-color="#ffffff" stop-opacity="0" />
        </linearGradient>
      </defs>
      <rect x="4" y="6" width="56" height="52" rx="16" fill="url(#mdw-logo-bg)" />
      <rect x="4" y="6" width="56" height="52" rx="16" fill="url(#mdw-logo-shine)" />
      <rect x="5.5" y="7.5" width="53" height="49" rx="14.5" fill="none" stroke="white" stroke-opacity="0.45" />
      <text
        x="32"
        y="38"
        text-anchor="middle"
        fill="white"
        font-family="Manrope, Inter, sans-serif"
        font-size="15"
        font-weight="700"
        letter-spacing="0.5"
      >MDW</text>
    </svg>
    <span class="text-xs font-semibold font-manrope tracking-[0.28em] text-white/90">SGC</span>
  </div>
</template>
