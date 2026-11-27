.. SPDX-FileCopyrightText: 2026 Froststrap
..
.. SPDX-License-Identifier: MPL-2.0

Contributing to Froststrap
==========================

Thank you for showing interest in contributing to Froststrap!

This guide covers how to build the project, what we accept, and what we expect from pull requests and issues.
If something here is unclear or out of date, ask on our `Discord server <https://discord.gg/KdR9vpRcUN>`_.

Quick start
~~~~~~~~~~~

Required toolchains:

- Rust (2024 edition)
- .NET SDK 10
- On macOS: Xcode Command Line Tools & macOS 12 or newer

If you use Nix, the flake at the repository root provides a development shell::

    nix develop

The build is driven by a Rust ``xtask`` crate in ``build/``::

    cd build
    cargo xtask restore
    cargo xtask build
    cargo xtask publish --no-installers   # optional, produces a publishable build without installers

Useful flags include ``--configuration`` and ``--override-arch``. Run ``cargo xtask --help`` for the full list.

The full, up-to-date build guide (dependencies, commands, and file outputs) lives at
https://docs.froststrap.xyz/docs/contributing/build-from-source

.. note::

   The repository layout is currently being reorganised, so this guide intentionally avoids describing
   individual directories. If you are unsure where something belongs, ask before you start.

The native backend
~~~~~~~~~~~~~~~~~~

Most of Froststrap is C#. The Rust native backend (``backend/``) exists for the cases where Rust clearly
earns its place, for example:

- Performance-sensitive code
- Functionality where the library support in .NET is poor or missing
- Messy platform interop, where a native abstraction layer gives C# a clean interface instead of a pile of
  P/Invoke

If C# can do the job reasonably well, it should stay in C#. Moving code into the backend "because Rust" is not
a good enough reason, so explain why the native side is the better fit in your pull request.

Accepted contributions
~~~~~~~~~~~~~~~~~~~~~~

Rather than listing everything we allow, it's easier to list what we don't accept:

- **Fully AI-generated changes.** Using AI tools is fine, but you must understand every line you submit and be
  able to explain and defend it during review. Changes with no human in the loop will be closed.
- **Trivial changes with no real benefit.** For example, pure style churn or a one-line syntax change with no
  functional or readability impact. Small fixes for real bugs are welcome, however small the diff.

For large changes (new subsystems, rewrites, refactors spanning many files), please open an issue or start a
discussion **before** you begin. That way your work doesn't clash with something already in
progress, and nobody spends a week on a change that can't be merged.

Pull requests
~~~~~~~~~~~~~

Checks
------

Before opening a pull request, make sure the following pass:

- **Spell checking:** the ``typos`` CLI tool is recommended
- **Licensing:** run ``reuse lint``. New files need SPDX headers, which you can add with::

      reuse annotate --copyright="Froststrap" --license="MPL-2.0" <file>

- **Formatting:** ``cargo fmt``, ``nix fmt``, and ``dotnet format``

Don't get in the way of other work
----------------------------------

Your change should not:

- Introduce new warnings or errors
- Block or complicate other features from being implemented (for example, bundling a rewrite of a subsystem
  into a PR that is only meant to add a feature)

Keep each pull request focused on one logical change.

Testing
-------

There is no automated test suite at the moment, so testing is manual. In your pull request description, say:

- What you tested and how
- Which operating system(s) you tested on

If you changed platform-specific code you couldn't test yourself (for example, macOS code without a Mac),
say so clearly. That's fine, but we need to know.

Titles, descriptions, and the changelog
---------------------------------------

Pull requests are **squash-merged**, and our changelog is generated from the resulting commit messages. So:

- Give your pull request a clear, descriptive title that summarises the change
- In the description, explain **why** the change was made, not just what changed, especially for anything
  user-facing
- Link any related issues

You don't need to follow a specific commit message format for the commits inside your branch.

Licensing
---------

Froststrap is licensed under the MPL-2.0. By contributing, you agree that your contributions are licensed
under the same terms. There is no CLA or sign-off requirement.

Issues
~~~~~~

When creating an issue, you agree to the following:

- **Fill in the issue form accurately.** Issues with incorrect or misleading information (such as ticked
  checkboxes that aren't true) may be closed, and repeated misuse can lead to a block.
- **No duplicate issues.** Search existing issues first, including closed ones. If a previously fixed issue
  reappears in a later release, ask for the original issue to be reopened instead of opening a new one.
- **Not everything needs to be an issue.** Questions, ideas, and general discussion often fit better on our
  `Discord server <https://discord.gg/KdR9vpRcUN>`_.
