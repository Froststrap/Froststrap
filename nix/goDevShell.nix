{
  mkShell,
  go,
  gopls,
  pkg-config,
  gcc,
}:
mkShell {
  buildInputs = [
    go
    gopls
    gcc
  ];

  nativeBuildInputs = [
    pkg-config
  ];
}
