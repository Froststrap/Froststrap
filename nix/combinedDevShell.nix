{
  typos,
  inputs,
  mkShell,
  callPackage,
}:
mkShell {
  inputsFrom = [
    (callPackage ./dotnetDevShell.nix { })
    (callPackage ./rustDevShell.nix { inherit inputs; })
    (callPackage ./goDevShell.nix { })
  ];

  buildInputs = [
    typos
  ];
}
