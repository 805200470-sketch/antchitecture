// Yield to the browser between generation steps. A visible tab yields on the next
// animation frame, so the loading bar paints. A hidden tab — where rAF pauses and
// chained timers are throttled to as little as once a minute — yields through a
// MessageChannel, which is not throttled, so loading keeps going in the background.
const channel = typeof MessageChannel !== 'undefined' ? new MessageChannel() : null;
const waiting = [];
if (channel) channel.port1.onmessage = () => waiting.shift()?.();

/** Resolves on the next frame (visible) or the next task (hidden). */
export function nextFrame() {
  return new Promise((resolve) => {
    if (typeof document !== 'undefined' && !document.hidden) {
      const t = setTimeout(resolve, 50); // the tab may be hidden before the frame comes
      requestAnimationFrame(() => {
        clearTimeout(t);
        resolve();
      });
    } else if (channel) {
      waiting.push(resolve);
      channel.port2.postMessage(0);
    } else {
      setTimeout(resolve, 0);
    }
  });
}
