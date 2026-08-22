{
  lib,
  mkShell
}:
{
  mkFragment = argsOrFn: let
    args = if lib.isFunction argsOrFn then lib.fix argsOrFn else argsOrFn;
  in {
    buildInputs = args.buildInputs or [];
    nativeBuildInputs = args.nativeBuildInputs or [];
    shellHook = args.shellHook or "";
  };

  mkComposedShell = frags: mkShell {
    buildInputs = lib.concatMap (f: f.buildInputs) frags;
    nativeBuildInputs = lib.concatMap (f: f.nativeBuildInputs) frags;
    shellHook = lib.concatStringsSep "\n" (map (f: f.shellHook) frags);
  };
}
