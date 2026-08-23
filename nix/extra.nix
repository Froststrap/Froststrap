{
  typos,
  callPackage,
  ... # capture inputs
}:
let
  inherit (callPackage ./devshell-tools.nix {}) mkFragment;
in
mkFragment {
  buildInputs = [
    typos
  ];
}
