# SPDX-FileCopyrightText: 2026 Froststrap
#
# SPDX-License-Identifier: MPL-2.0
{
  lib,
  stdenv,
  mkShell,
  mkShellNoCC,
}:
{
  mkFragment =
    argsOrFn:
    let
      args = if lib.isFunction argsOrFn then lib.fix argsOrFn else argsOrFn;
    in
    {
      name = args.name or "";
      buildInputs = args.buildInputs or [ ];
      nativeBuildInputs = args.nativeBuildInputs or [ ];
      shellHook = args.shellHook or "";
    };

  mkComposedShell =
    frags:
    {
      nameOverride ? null,
    }:
    let
      fragNames = lib.filter (n: n != null) (map (f: f.name or null) frags);
      guessedName = if fragNames == [ ] then "default" else lib.concatStringsSep "-" fragNames;
      setName = if nameOverride != null then nameOverride else guessedName;
    in
    {
      ${setName} = (if stdenv.hostPlatform.isDarwin then mkShellNoCC else mkShell) {
        buildInputs = lib.concatMap (f: f.buildInputs) frags;
        nativeBuildInputs = lib.concatMap (f: f.nativeBuildInputs) frags;
        shellHook = lib.concatStringsSep "\n" (map (f: f.shellHook) frags);
      };
    };
}
