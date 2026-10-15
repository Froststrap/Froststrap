use std::{
    fs,
    os::unix::fs::PermissionsExt,
    path::{Path, PathBuf},
};
use pissing::pl::PListFile;
use crate::utils::{log, process};

fn copy_any(src: &Path, dest: &Path) {
    let meta = fs::symlink_metadata(src).unwrap();
    if meta.file_type().is_symlink() {
        std::os::unix::fs::symlink(fs::read_link(src).unwrap(), dest).unwrap();
    } else if meta.is_dir() {
        fs::create_dir_all(dest).unwrap();
        for e in fs::read_dir(src).unwrap() {
            let e = e.unwrap();
            copy_any(&e.path(), &dest.join(e.file_name()));
        }
    } else {
        fs::create_dir_all(dest.parent().unwrap()).unwrap();
        fs::copy(src, dest).unwrap();
    }
}

fn compile_png_to_icns(png_path: &Path, out_icns_path: &Path) {
    let temp_dir = std::env::temp_dir().join(format!("iconset_{}", std::process::id()));
    let iconset_dir = temp_dir.join("app.iconset");
    fs::create_dir_all(&iconset_dir).unwrap();

    let sizes = [
        (16, "icon_16x16.png"),
        (32, "icon_16x16@2x.png"),
        (32, "icon_32x32.png"),
        (64, "icon_32x32@2x.png"),
        (128, "icon_128x128.png"),
        (256, "icon_128x128@2x.png"),
        (256, "icon_256x256.png"),
        (512, "icon_256x256@2x.png"),
        (512, "icon_512x512.png"),
        (1024, "icon_512x512@2x.png"),
    ];

    for (size, filename) in sizes {
        let out_file = iconset_dir.join(filename);
        let size_str = size.to_string();

        process(
            "sips",
            [
                "-z",
                &size_str,
                &size_str,
                png_path.to_str().unwrap(),
                "--out",
                out_file.to_str().unwrap(),
            ],
        )
        .exec().map(|x| x.stderr).unwrap();
    }

    process(
        "iconutil",
        [
            "--convert",
            "icns",
            iconset_dir.to_str().unwrap(),
            "--output",
            out_icns_path.to_str().unwrap(),
        ],
    )
    .exec_checked("iconutil");

    let _ = fs::remove_dir_all(&temp_dir);
}

#[derive(Clone)]
pub enum Resource {
    Binary,
    Framework,
    Resource,
}

impl Resource {
    pub fn dir(self) -> &'static str {
        match self {
            Self::Binary => "MacOS",
            Self::Framework => "Frameworks",
            Self::Resource => "Resources",
        }
    }

    pub fn disp(self) -> &'static str {
        match self {
            Self::Binary => "binary",
            Self::Framework => "framework",
            Self::Resource => "resource",
        }
    }
}

pub struct AppBuilder {
    name: String,
    info_plist: Option<PListFile>,
    app_icon_png: Option<PathBuf>,
    items: Vec<(PathBuf, Resource, String)>,
}

impl AppBuilder {
    pub fn new(name: &str) -> Self {
        Self {
            name: name.into(),
            info_plist: None,
            app_icon_png: None,
            items: vec![],
        }
    }

    pub fn info_plist(mut self, plist: PListFile) -> Self {
        self.info_plist = Some(plist);
        self
    }

    pub fn app_icon(mut self, path: impl Into<PathBuf>) -> Self {
        self.app_icon_png = Some(path.into());
        self
    }

    fn include_item(mut self, path: impl Into<PathBuf>, kind: Resource) -> Self {
        let path = path.into();
        let name = path.file_name().unwrap().to_string_lossy().into_owned();
        self.items.push((path, kind, name));
        self
    }

    pub fn binary(self, path: impl Into<PathBuf>) -> Self {
        self.include_item(path, Resource::Binary)
    }

    pub fn framework(self, path: impl Into<PathBuf>) -> Self {
        self.include_item(path, Resource::Framework)
    }

    pub fn resource(self, path: impl Into<PathBuf>) -> Self {
        self.include_item(path, Resource::Resource)
    }

    pub fn frameworks<I, P>(mut self, ps: I) -> Self
    where
        I: IntoIterator<Item = P>,
        P: Into<PathBuf>,
    {
        for p in ps {
            self = self.framework(p);
        }
        self
    }

    pub fn build(self, out_dir: &Path) -> PathBuf {
        let app = out_dir.join(format!("{}.app", self.name));
        log::info!("Creating .app bundle to {}", app.to_string_lossy());

        let contents = app.join("Contents");
        if app.is_dir() {
            fs::remove_dir_all(&app).unwrap();
        }

        let resources_dir = contents.join("Resources");
        fs::create_dir_all(&contents).unwrap();
        fs::create_dir_all(&resources_dir).unwrap();
        fs::create_dir_all(contents.join("MacOS")).unwrap();
        fs::create_dir_all(contents.join("Frameworks")).unwrap();

        let plist = self.info_plist.expect("info_plist is required");

        if let Some(png_path) = self.app_icon_png {
            let target_icns = resources_dir.join("AppIcon.icns");
            log::info!("Compiling {} into AppIcon.icns", png_path.display());

            compile_png_to_icns(&png_path, &target_icns);
        }

        for (src, kind, name) in &self.items {
            let kind_disp = kind.clone().disp();
            log::info!("Stitching {kind_disp} {} to {}", name, kind.clone().dir());
            let dest_dir = contents.join(kind.clone().dir());
            fs::create_dir_all(&dest_dir).unwrap();
            let dest = dest_dir.join(name);
            copy_any(src, &dest);

            if matches!(kind, Resource::Binary) {
                let mut perms = fs::metadata(&dest).unwrap().permissions();
                perms.set_mode(0o755);
                fs::set_permissions(&dest, perms).unwrap();
            }
        }

        // 3. Write Info.plist and PkgInfo
        plist
            .write(contents.join("Info.plist"))
            .expect("Failed to write Info.plist");

        fs::write(contents.join("PkgInfo"), "APPL????").unwrap();

        app
    }
}
