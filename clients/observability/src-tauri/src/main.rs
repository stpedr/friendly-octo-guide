// Ponto de entrada desktop do cliente de observabilidade — o núcleo está em lib.rs
// (compartilhado com o mobile), mesmo padrão do shell principal.
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

fn main() {
    plataforma_linha_obs_lib::run();
}
