//! Narrate Everything in Date Everything!: what the shared installer needs to know and do.
//!
//! Install: every file in the payload is written into the game folder (the mod and its data
//! files, the BepInEx mod loader, prism.dll for speech and its licences, the readme). What
//! the install added at the top of the game folder is recorded in
//! NarrateEverything-installed.txt, so uninstall removes exactly that and nothing the player
//! had before. No game file is changed: BepInEx loads through its own winhttp.dll.
use std::fs;
use std::path::Path;

use magpie_installer::files::{self, rel_path, Record};
use magpie_installer::{sys, Choices, Info, Mod, Payload};

/// Present while the mod is installed.
const INSTALLED_MARKER: &str = "BepInEx/plugins/DateEverythingAccess.dll";
/// Where the installed mod version is recorded; removed on uninstall.
const VERSION_FILE: &str = "BepInEx/plugins/NarrateEverything-version.txt";
/// What the install added at the top of the game folder, one name per line.
const RECORD: &str = "NarrateEverything-installed.txt";
const README_NAME: &str = "NarrateEverything-README.txt";
/// The mod's own files, removed on uninstall even where their folders were there before
/// (BepInEx, for one, may have been installed for another mod). The settings and the
/// known-objects history the mod writes go too.
const OWN: &[&str] = &[
    INSTALLED_MARKER,
    VERSION_FILE,
    "BepInEx/plugins/card_pose_descriptions.en.json",
    "BepInEx/plugins/navigable_region.bake.json",
    "BepInEx/plugins/examine_history",
    "BepInEx/plugins/interact_history",
    "BepInEx/config/com.amock.dateeverythingaccess.cfg",
    "prism.dll",
    "Prism-Licenses",
    README_NAME,
    "NarrateEverything-THIRD-PARTY-NOTICES.txt",
];
/// What the earlier setup (NarrateEverything-Setup.exe and its test builds) put in the
/// game folder under generic names: removed on install, but only if it is that setup's.
const OLD_SETUP_FILES: &[(&str, &[&str])] = &[
    ("README.txt", &["Narrate Everything", "Date Everything Access - Navigation Test Build"]),
    ("THIRD-PARTY-NOTICES.txt", &["Date Everything Access Portable Package"]),
];

pub struct NarrateEverything {
    info: Info,
    payload: &'static Payload<'static>,
}

impl NarrateEverything {
    pub fn new(payload: &'static Payload<'static>) -> NarrateEverything {
        NarrateEverything {
            info: Info {
                mod_name: "Narrate Everything",
                mod_version: payload.mod_version,
                game_name: "Date Everything",
                game_version: "",
                game_exe: "Date Everything.exe",
                purpose: "screen reader accessibility",
                launch_note: "Start the game. You should hear \"Narrate Everything loaded. Press F-1 for \
                              accessibility help.\" The keys are in NarrateEverything-README.txt in the game \
                              folder. To remove the mod, run this installer again.",
                readme: Some((README_NAME, payload.readme)),
                options: vec![],
            },
            payload,
        }
    }
}

/// Removes a file the earlier setup wrote, if it begins with one of that setup's titles.
fn remove_old_setup_file(game: &Path, name: &str, titles: &[&str]) -> bool {
    let path = rel_path(game, name);
    let Ok(text) = fs::read_to_string(&path) else { return false };
    let first = text.trim_start_matches('\u{feff}').lines().next().unwrap_or("").trim();
    titles.iter().any(|t| first == *t) && fs::remove_file(&path).is_ok()
}

impl Mod for NarrateEverything {
    fn info(&self) -> &Info {
        &self.info
    }

    /// "unknown" for an install that didn't record its version (installed by hand, by the
    /// original mod's package, or by the earlier NarrateEverything-Setup.exe).
    fn installed_version(&self, game: &Path) -> Option<String> {
        if !rel_path(game, INSTALLED_MARKER).exists() {
            return None;
        }
        Some(
            fs::read_to_string(rel_path(game, VERSION_FILE))
                .map(|s| s.trim().to_string())
                .ok()
                .filter(|s| !s.is_empty())
                .unwrap_or_else(|| "unknown".into()),
        )
    }

    fn install_summary(&self, _choices: &Choices) -> String {
        "No game file is changed; the mod and its loader are added beside them. Your mod settings \
         and the list of objects you've found are kept."
            .to_string()
    }

    fn uninstall_summary(&self) -> String {
        "The mod, its settings and its speech files are deleted, and the mod loader too if this \
         installer added it. The game itself is not touched."
            .to_string()
    }

    fn install(&self, game: &Path, _choices: &Choices, log: &dyn Fn(&str)) -> Result<Vec<String>, String> {
        // What this install adds at the top of the game folder (kept from an earlier install).
        let mut record = Record::read(game, RECORD);
        files::unpack(self.payload, game, &mut record, log)?;

        for (name, titles) in OLD_SETUP_FILES {
            if remove_old_setup_file(game, name, titles) {
                log(&format!("Removed {name} from the earlier setup."));
            }
        }

        if let Err(e) = fs::write(rel_path(game, VERSION_FILE), self.payload.mod_version) {
            log(&format!("Warning: couldn't record the installed version ({e})."));
        }

        OWN.iter().for_each(|own| record.add(own));
        record.write()?;

        let mut notes = vec![];
        if sys::is_wine() {
            // BepInEx loads through the game's winhttp.dll, which Wine would otherwise
            // replace with its own.
            notes.push(if sys::wine_dll_override("winhttp", "native,builtin") {
                "CrossOver/Wine: set winhttp.dll to \"native, builtin\" in this bottle, so the mod loader can start.".to_string()
            } else {
                "CrossOver/Wine: couldn't change the bottle's settings. In its Wine configuration, under Libraries, \
                 set \"winhttp\" to \"native, builtin\", or the mod won't load."
                    .to_string()
            });
        }
        Ok(notes)
    }

    fn uninstall(&self, game: &Path, log: &dyn Fn(&str)) -> Result<Vec<String>, String> {
        let mut warnings = vec![];
        let mut record = Record::read(game, RECORD);
        if record.is_empty() {
            // Installed by hand or by an earlier setup: only the mod itself is known to be ours.
            warnings.push(
                "There's no record of what was installed here, so only the mod itself was removed. \
                 The mod loader (BepInEx) was left in place."
                    .to_string(),
            );
        }
        OWN.iter().for_each(|own| record.add(own));
        record.add(RECORD);
        log("Removing the mod's files...");
        files::remove(game, &record)?;
        Ok(warnings)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use magpie_installer::payload::archive;

    const FILES: &[(&str, &[u8])] = &[
        ("winhttp.dll", b"doorstop"),
        ("doorstop_config.ini", b"[General]"),
        (".doorstop_version", b"4.3"),
        ("BepInEx/core/BepInEx.dll", b"loader"),
        ("BepInEx/plugins/DateEverythingAccess.dll", b"the mod"),
        ("BepInEx/plugins/navigable_region.bake.json", b"{}"),
        ("BepInEx/plugins/card_pose_descriptions.en.json", b"{}"),
        ("prism.dll", b"speech"),
        ("Prism-Licenses/NOTICE.txt", b"licences"),
        ("NarrateEverything-README.txt", b"keys"),
        ("NarrateEverything-THIRD-PARTY-NOTICES.txt", b"notices"),
    ];

    /// A fake game: Date Everything.exe in a folder; the payload holds a few stand-in files.
    fn fake() -> (tempfile::TempDir, NarrateEverything) {
        let mut entries = FILES.iter().map(|(n, b)| (n.to_string(), b.to_vec()));
        let packed = archive::pack(&mut entries, Vec::new(), 1 << 20).unwrap();
        let payload: &'static Payload<'static> = Box::leak(Box::new(Payload {
            mod_version: "1.0.1",
            archive: Box::leak(packed.into_boxed_slice()),
            file_count: FILES.len(),
            unpacked_size: FILES.iter().map(|(_, b)| b.len() as u64).sum(),
            readme: "keys",
        }));
        let dir = tempfile::tempdir().unwrap();
        fs::write(dir.path().join("Date Everything.exe"), "").unwrap();
        fs::create_dir(dir.path().join("Date Everything_Data")).unwrap();
        fs::write(dir.path().join("Date Everything_Data/level0"), "game data").unwrap();
        (dir, NarrateEverything::new(payload))
    }

    fn none() -> Choices {
        Choices(vec![])
    }

    #[test]
    fn install_copies_everything_and_records_it() {
        let (dir, m) = fake();
        let g = dir.path();
        assert!(m.is_game_dir(g));
        assert_eq!(m.installed_version(g), None);
        m.install(g, &none(), &|_| {}).unwrap();
        for (rel, bytes) in FILES {
            assert_eq!(fs::read(rel_path(g, rel)).unwrap(), *bytes, "{rel}");
        }
        assert_eq!(m.installed_version(g).as_deref(), Some("1.0.1"));
        let record = Record::read(g, RECORD);
        for top in ["winhttp.dll", "BepInEx", "prism.dll", README_NAME, RECORD] {
            assert!(record.items.contains(top), "{top} not recorded: {:?}", record.items);
        }
        assert!(!record.items.contains("Date Everything_Data"));
    }

    #[test]
    fn update_keeps_settings_and_history() {
        let (dir, m) = fake();
        let g = dir.path();
        m.install(g, &none(), &|_| {}).unwrap();
        fs::create_dir_all(g.join("BepInEx/config")).unwrap();
        fs::write(g.join("BepInEx/config/com.amock.dateeverythingaccess.cfg"), "AimingHelp = Tone").unwrap();
        fs::write(g.join("BepInEx/plugins/examine_history"), "found").unwrap();
        m.install(g, &none(), &|_| {}).unwrap();
        assert_eq!(fs::read_to_string(g.join("BepInEx/config/com.amock.dateeverythingaccess.cfg")).unwrap(), "AimingHelp = Tone");
        assert_eq!(fs::read_to_string(g.join("BepInEx/plugins/examine_history")).unwrap(), "found");
    }

    #[test]
    fn install_removes_the_old_setup_s_readme_only() {
        let (dir, m) = fake();
        let g = dir.path();
        fs::write(g.join("README.txt"), "Narrate Everything \r\nBased on Date Everything Access 1.2\r\n").unwrap();
        fs::write(g.join("THIRD-PARTY-NOTICES.txt"), "Date Everything Access Portable Package\r\n").unwrap();
        m.install(g, &none(), &|_| {}).unwrap();
        assert!(!g.join("README.txt").exists() && !g.join("THIRD-PARTY-NOTICES.txt").exists());

        // Somebody else's readme stays.
        fs::write(g.join("README.txt"), "Date Everything Access\r\nthe original mod\r\n").unwrap();
        m.install(g, &none(), &|_| {}).unwrap();
        assert!(g.join("README.txt").exists());
    }

    #[test]
    fn uninstall_removes_what_was_added_and_nothing_else() {
        let (dir, m) = fake();
        let g = dir.path();
        m.install(g, &none(), &|_| {}).unwrap();
        // What BepInEx and the mod make while the game runs.
        fs::write(g.join("BepInEx/LogOutput.log"), "log").unwrap();
        fs::create_dir_all(g.join("BepInEx/config")).unwrap();
        fs::write(g.join("BepInEx/config/com.amock.dateeverythingaccess.cfg"), "settings").unwrap();
        let warnings = m.uninstall(g, &|_| {}).unwrap();
        assert!(warnings.is_empty(), "{warnings:?}");
        let left: Vec<String> = fs::read_dir(g).unwrap().flatten().map(|e| e.file_name().to_string_lossy().into_owned()).collect();
        assert_eq!(left.len(), 2, "{left:?}");
        assert_eq!(m.installed_version(g), None);
    }

    #[test]
    fn uninstall_leaves_a_mod_loader_that_was_there_before() {
        let (dir, m) = fake();
        let g = dir.path();
        fs::create_dir_all(g.join("BepInEx/plugins")).unwrap();
        fs::write(g.join("BepInEx/plugins/Other.dll"), "another mod").unwrap();
        fs::write(g.join("winhttp.dll"), "theirs").unwrap();
        m.install(g, &none(), &|_| {}).unwrap();
        m.uninstall(g, &|_| {}).unwrap();
        assert!(g.join("BepInEx/plugins/Other.dll").exists() && g.join("winhttp.dll").exists());
        assert!(!g.join(INSTALLED_MARKER).exists() && !g.join("prism.dll").exists());
    }

    #[test]
    fn uninstall_without_a_record_removes_only_the_mod() {
        let (dir, m) = fake();
        let g = dir.path();
        m.install(g, &none(), &|_| {}).unwrap();
        fs::remove_file(g.join(RECORD)).unwrap();
        let warnings = m.uninstall(g, &|_| {}).unwrap();
        assert_eq!(warnings.len(), 1);
        assert_eq!(m.installed_version(g), None);
        assert!(g.join("BepInEx/core/BepInEx.dll").exists() && g.join("winhttp.dll").exists());
    }

    #[test]
    fn embedded_payload_is_complete() {
        let p = &crate::PAYLOAD;
        let mut names = Vec::new();
        let mut total = 0;
        archive::unpack(p.archive, &mut |name, _, size| {
            names.push(name.to_string());
            total += size;
            Ok(())
        })
        .unwrap();
        assert_eq!((names.len(), total), (p.file_count, p.unpacked_size));
        for f in ["winhttp.dll", "doorstop_config.ini", "BepInEx/core/BepInEx.dll", INSTALLED_MARKER,
                  "BepInEx/plugins/navigable_region.bake.json", "prism.dll", README_NAME] {
            assert!(names.iter().any(|n| n == f), "{f} missing");
        }
        assert!(names.iter().all(|n| files::safe_rel(n)));
        assert!(!p.readme.is_empty());
    }
}
