Building Froststrap
===================

This is basic documentation on how to build Froststrap from source, with commands.

Dependencies
------------

This wont be is accurate to what is needed to be installed for it to build succesfully,
but more so the acknowlegable ones.

It is way easier if you are on macOS or Linux to just use Nix, and enter the flake devshell with

.. code-block::

   nix develop

and if you get prompted to trust nix configuraiton stuff, you can deny if you don't feel it's safe.

macOS Specific
~~~~~~~~~~~~~~

- Xcode (.app is preferred as it has the whole toolchain which is needed)
- Xcode Command Line Tools
- Swift compiler (should come with Xcode Command Line Utils)

Windows Specific
~~~~~~~~~~~~~~~~

- NSIS (need to add the NSIS compiler to PATH env, which can be found via searching "Edit System Environment Variables")

All other dependents
~~~~~~~~~~~~~~~~~~~~

- Rust compiler (Rust 2024, and preferrably though rustup)
- .NET 10 SDK (Need a C# compiller)
- Fallout dotnet tool (build ochestration system)
- Git (needed by Fallout & our orchestration to publish with a version, which is nedeed to publish)

Commands
--------

Publish
~~~~~~~

Publishing is going to create installers, and other important stuff making it publishable online.

.. source-code::

   dotnet run --project build -- publish --configuration Release

Build
~~~~~

Going to be useful for debug builds, and there's mulitple ways to do so.

.. source-code::

   dotnet run --project build -- compile --configuration Release

.. source-code::

   dotnet build

Clean
~~~~~

Cleaning out stale stuff- should run both of these.

.. source-code::

   dotnet run --project build -- clean --configuration Release

.. source-code::

   dotnet clean
