// Núcleo compartilhado do shell: desktop (src/main.rs) e mobile (entry point
// abaixo) chamam o mesmo run() — a única diferença entre plataformas é quem
// invoca. Nenhuma lógica de produto mora aqui; o core é clients/pwa.
use tauri::{WebviewUrl, WebviewWindowBuilder};

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_store::Builder::default().build())
        .setup(|app| {
            // Desktop lê do ambiente; mobile não tem shell pra exportar env var,
            // então cai no default — trocar por uma tela de configuração é o
            // próximo passo natural (endereço do Gateway não é fixo na rede doméstica).
            let gateway_url = std::env::var("PLATAFORMA_GATEWAY_URL")
                .unwrap_or_else(|_| "http://localhost:8180".to_string());
            let init_script = format!("window.GATEWAY_URL = {gateway_url:?};");

            WebviewWindowBuilder::new(app, "main", WebviewUrl::App("index.html".into()))
                .title("plataforma-linha")
                .inner_size(1100.0, 760.0)
                .min_inner_size(480.0, 640.0)
                .initialization_script(&init_script)
                .build()?;

            Ok(())
        })
        .run(tauri::generate_context!())
        .expect("erro ao rodar o shell da plataforma-linha");
}
