{
  typos,
  nushell,
  callPackage
}:
let
  inherit (callPackage ./devshell-tools.nix {}) mkFragment;
in
mkFragment {
  buildInputs = [
    nushell
    typos
  ];
}
