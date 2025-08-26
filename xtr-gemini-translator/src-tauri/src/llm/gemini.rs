use anyhow::Result;
use reqwest::{Client, StatusCode};
use serde_json::json;
use std::time::Duration;
#[derive(thiserror::Error, Debug)]
pub enum LlmCallError {
  #[error("rate_limited")]
  RateLimited { retry_after: Option<Duration> },
  #[error("server_error {0}")]
  Server(u16),
  #[error("client_error {0}")]
  Client(u16),
  #[error("network {0}")]
  Network(String),
  #[error("decode {0}")]
  Decode(String),
}
pub struct Gemini {
  client: Client,
  #[allow(unused)]
  api_key: String,
  #[allow(unused)]
  model: String,
  #[allow(unused)]
  temperature: f32,
}
impl Gemini {
  pub fn new(client: Client, api_key: String, model: String, temperature: f32) -> Self {
    Self { client, api_key, model, temperature }
  }
  pub async fn translate_batch(&self, chunks: &[String], _system_prompt: &str) -> std::result::Result<Vec<String>, LlmCallError> {
    let sentinel = "\n<<<#SEP#>>>\n";
    let text = chunks.join(sentinel);
    let prompt = format!("{}\n\n{}", _system_prompt, text);
    let body = json!({
      "contents": [{
        "role": "user",
        "parts": [{ "text": prompt }]
      }],
      "generationConfig": { "temperature": self.temperature }
    });
    let url = format!(
      "https://generativelanguage.googleapis.com/v1beta/models/{}:generateContent",
      self.model
    );
    let resp = self.client
      .post(url)
      .header("x-goog-api-key", &self.api_key)
      .json(&body)
      .send().await
      .map_err(|e| LlmCallError::Network(e.to_string()))?;
    let status = resp.status();
    if !status.is_success() {
      // check Retry-After for 429/503
      let retry_after = if status == StatusCode::TOO_MANY_REQUESTS || status == StatusCode::SERVICE_UNAVAILABLE {
        if let Some(h) = resp.headers().get("retry-after") {
          if let Ok(s) = h.to_str() {
            if let Ok(sec) = s.parse::<u64>() {
              Some(Duration::from_secs(sec))
            } else { None }
          } else { None }
        } else { None }
      } else { None };
      return Err(match status.as_u16() {
        429 => LlmCallError::RateLimited { retry_after },
        500..=599 => LlmCallError::Server(status.as_u16()),
        _ => LlmCallError::Client(status.as_u16()),
      });
    }
    let resp_json = resp.json::<serde_json::Value>().await
      .map_err(|e| LlmCallError::Decode(e.to_string()))?;
    let out_text = resp_json["candidates"][0]["content"]["parts"][0]["text"]
      .as_str()
      .unwrap_or("")
      .to_string();
    let items: Vec<String> = if out_text.is_empty() {
      chunks.iter().cloned().collect()
    } else {
      out_text.split(sentinel).map(|s| s.to_string()).collect()
    };
    Ok(items)
  }
}
