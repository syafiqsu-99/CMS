export function usePolling(fn, intervalMs) {
  let timer = null;
  let running = false;

  async function tick() {
    if (running) return;
    running = true;
    try {
      await fn();
    } catch (err) {
      console.error('[usePolling] callback error:', err);
    } finally {
      running = false;
    }
  }

  function start() {
    if (timer !== null) return;
    timer = setInterval(tick, intervalMs);
  }

  function stop() {
    if (timer !== null) {
      clearInterval(timer);
      timer = null;
    }
  }

  return { start, stop };
}
