// Ponto de entrada do desktop — todo o resto é compartilhado com o mobile em lib.rs.
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

fn main() {
    plataforma_linha_lib::run();
}
