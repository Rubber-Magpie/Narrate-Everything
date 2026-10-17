//! NarrateEverythingInstaller.exe: installs, updates or removes Narrate Everything, the
//! screen reader accessibility mod for Date Everything!. Everything it installs is inside
//! the exe (see build.rs), for one mod version. The window and the command line are the
//! installer all Rubber Magpie mods share (magpie-installer); game.rs is what's this mod's.
//!
//!   NarrateEverythingInstaller.exe                  the window
//!   NarrateEverythingInstaller.exe --cli            the same in a terminal
//!   NarrateEverythingInstaller.exe --install   [--game DIR]   no questions (scripts)
//!   NarrateEverythingInstaller.exe --uninstall [--game DIR]
#![windows_subsystem = "windows"]

mod game;

include!(concat!(env!("OUT_DIR"), "/payload.rs"));

fn main() {
    magpie_installer::run(game::NarrateEverything::new(&PAYLOAD));
}
