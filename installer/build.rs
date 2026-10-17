// Packs everything the installer carries, so the exe is the whole release: every file for
// the game folder, from the staging folder that tools/package.ps1 fills (the mod and its
// data files, BepInEx, prism.dll and its licences, the readme and notices).
//
// Inputs (environment):
//   NE_STAGE     the staging folder (default ../dist/stage).
//   NE_VERSION   the mod version (default today, YYYY.MM.DD).
use magpie_payload::build;

fn main() {
    println!("cargo:rerun-if-changed=build.rs");
    build::windows_manifest();
    let stage = build::env_path("NE_STAGE", "../dist/stage");
    let version = build::env_or("NE_VERSION", &build::today());
    build::pack_stage(&stage, &version, "NarrateEverything-README.txt");
}
