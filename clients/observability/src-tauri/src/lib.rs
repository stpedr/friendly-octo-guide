// Núcleo do shell de observabilidade — desktop (main.rs) e mobile chamam o mesmo
// run(). Push nativo via tauri-plugin-notification (alerta do Alertmanager/ntfy
// chega como notificação do SO); o endereço do Grafana entra no window pelo
// init script, como o Gateway no shell principal. Nenhuma lógica de produto aqui.
use tauri::{WebviewUrl, WebviewWindowBuilder};

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_store::Builder::default().build())
        .plugin(tauri_plugin_notification::init()) // push nativo de alerta
        .setup(|app| {
            let grafana_url = std::env::var("PLATAFORMA_GRAFANA_URL")
                .unwrap_or_else(|_| "http://localhost:3000".to_string());
            let init_script = format!("window.GRAFANA_URL = {grafana_url:?};");

            WebviewWindowBuilder::new(app, "main", WebviewUrl::App("index.html".into()))
                .title("Observabilidade · Plataforma de Linha")
                .inner_size(1200.0, 800.0)
                .min_inner_size(480.0, 640.0)
                .initialization_script(&init_script)
                .build()?;

            Ok(())
        })
        .run(tauri::generate_context!())
        .expect("erro ao rodar o shell de observabilidade");
}
