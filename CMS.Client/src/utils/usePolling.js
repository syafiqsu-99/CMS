import { ref, onUnmounted } from 'vue';

export function usePolling(fn, intervalMs = 10_000) {
  const running = ref(false);
  let timer = null;
  let pending = false;

  async function tick() {
    if (pending) return;
    pending = true;
    try {
      await fn();
    } finally {
      pending = false;
    }
  }

  function start() {
    if (running.value) return;
    running.value = true;
    tick();
    timer = setInterval(tick, intervalMs);
  }

  function stop() {
    running.value = false;
    clearInterval(timer);
    timer = null;
  }

  onUnmounted(stop);

  return { start, stop, running };
}
