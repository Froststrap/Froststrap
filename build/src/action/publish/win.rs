//! os specific stuff for publish

use crate::args;

pub fn main() {
    let args = args::get();
    if !args.no_installers {
        super::velopack::pack(&crate::path::dotnet_publish_artifacts());
    }
}
