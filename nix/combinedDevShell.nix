{
  typos,
  mkShell,
  callPackage
}:
mkShell {
  inputsFrom = [
    (callPackage ./dotnetDevShell.nix { })
    (callPackage ./goDevShell.nix { })
  ];

  buildInputs = [
    typos
  ];
}
