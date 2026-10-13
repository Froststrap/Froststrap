use plist::Value;

use std::collections::BTreeMap;
use crate::bundle::macos::InfoPlist;

fn dict(entries: &[(&str, Value)]) -> Value {
    let mut map = BTreeMap::new();
    for (k, v) in entries {
        map.insert(k.to_string(), v.clone());
    }
    Value::Dictionary(::plist::Dictionary::from_iter(map))
}

fn str_arr(items: &[&str]) -> Value {
    Value::Array(items.iter().map(|s| Value::String(s.to_string())).collect())
}

fn array(items: Vec<Value>) -> Value {
    Value::Array(items)
}

pub fn build_info_plist(version: &str) -> InfoPlist {
    let mut plist = InfoPlist::new("Froststrap", "Froststrap", "xyz.froststrap.desktop");

    plist
        .version(version)
        .icon("Release")
        .set("CFBundleDocumentTypes", array(vec![
            dict(&[
                ("CFBundleTypeName", "Roblox Place".into()),
                ("CFBundleTypeRole", "Viewer".into()),
                ("LSItemContentTypes", str_arr(&["xyz.froststrap.roblox.place"])),
            ]),
            dict(&[
                ("CFBundleTypeName", "Roblox Model".into()),
                ("CFBundleTypeRole", "Viewer".into()),
                ("LSItemContentTypes", str_arr(&["xyz.froststrap.roblox.model"])),
            ]),
        ]))
        .set("CFBundleURLTypes", array(vec![
            dict(&[
                ("CFBundleTypeRole", "Viewer".into()),
                ("CFBundleURLName", "xyz.froststrap.roblox".into()),
                ("CFBundleURLSchemes", str_arr(&[
                    "roblox",
                    "roblox-player",
                    "roblox-studio",
                    "roblox-studio-auth",
                ])),
            ]),
        ]))
        .set("UTExportedTypeDeclarations", array(vec![
            dict(&[
                ("UTTypeDescription", "Roblox Place".into()),
                ("UTTypeIdentifier", "xyz.froststrap.roblox.place".into()),
                ("UTTypeConformsTo", str_arr(&["public.data"])),
                ("UTTypeTagSpecification", dict(&[
                    ("public.filename-extension", str_arr(&["rbxl", "rbxlx"])),
                ])),
            ]),
            dict(&[
                ("UTTypeDescription", "Roblox Model".into()),
                ("UTTypeIdentifier", "xyz.froststrap.roblox.model".into()),
                ("UTTypeConformsTo", str_arr(&["public.data"])),
                ("UTTypeTagSpecification", dict(&[
                    ("public.filename-extension", str_arr(&["rbxm", "rbxmx"])),
                ])),
            ]),
        ]));

    plist
}
