pub fn exp_backoff_ms(attempt: usize, base_ms: u64, cap_ms: u64) -> u64 {
  if attempt == 0 { return base_ms.min(cap_ms); }
  let shift = (attempt.saturating_sub(1)).min(63) as u32;
  let mul = 1u64 << shift;
  base_ms.saturating_mul(mul).min(cap_ms)
}
pub fn add_jitter_ms(base: u64, jitter_ms: u64) -> u64 {
  if jitter_ms == 0 { return base; }
  let j = (rand::random::<u16>() as u64) % (jitter_ms + 1);
  base.saturating_add(j)
}
use tokio::sync::Mutex;
use tokio::time::{Instant, Duration, sleep};
use std::sync::Arc;
pub struct RateLimiter {
  last: Mutex<Instant>,
  gap: Duration,
}
impl RateLimiter {
  pub fn new_per_sec(rps: u32) -> Arc<Self> {
    let rps = rps.max(1);
    let gap = Duration::from_secs_f64(1.0 / (rps as f64));
    Arc::new(Self {
      last: Mutex::new(Instant::now().checked_sub(gap).unwrap_or(Instant::now())),
      gap,
    })
  }
  pub async fn wait(&self) {
    let mut last = self.last.lock().await;
    let now = Instant::now();
    let next = (*last + self.gap).max(now);
    let delay = next.saturating_duration_since(now);
    *last = next;
    drop(last);
    if delay > Duration::from_millis(0) {
      sleep(delay).await;
    }
  }
}
// Token bucket rate limiter: refill tokens at rate per second, capacity cap; wait until 1 token available.
pub struct TokenBucket {
  inner: Mutex<(Instant, f64)>, // (last_refill, tokens)
  rate: f64,                    // tokens per second
  cap: f64,                     // capacity
}
impl TokenBucket {
  pub fn new(rate_per_sec: f64, capacity: f64) -> Arc<Self> {
    Arc::new(Self {
      inner: Mutex::new((Instant::now(), capacity)),
      rate: rate_per_sec.max(0.0001),
      cap: capacity.max(1.0),
    })
  }
  pub async fn wait_one(&self) {
    loop {
      let mut guard = self.inner.lock().await;
      let now = Instant::now();
      let elapsed = now.duration_since(guard.0).as_secs_f64();
      // refill
      let mut tokens = (guard.1 + elapsed * self.rate).min(self.cap);
      *guard = (now, tokens);
      if tokens >= 1.0 {
        // consume one
        guard.1 -= 1.0;
        return;
      }
      drop(guard);
      // compute time to next token
      let wait_s = (1.0 - tokens).max(0.0) / self.rate;
      sleep(Duration::from_secs_f64(wait_s.min(1.0))).await;
    }
  }
}
#[cfg(test)]
mod tests {
  use super::*;
  #[test]
  fn backoff_doubles_and_caps() {
    assert_eq!(exp_backoff_ms(0, 500, 5000), 500);
    assert_eq!(exp_backoff_ms(1, 500, 5000), 500);
    assert_eq!(exp_backoff_ms(2, 500, 5000), 1000);
    assert_eq!(exp_backoff_ms(3, 500, 5000), 2000);
    assert_eq!(exp_backoff_ms(4, 500, 5000), 4000);
    assert_eq!(exp_backoff_ms(5, 500, 5000), 5000);
    assert_eq!(exp_backoff_ms(6, 500, 5000), 5000);
  }
}
