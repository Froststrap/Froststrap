.. SPDX-FileCopyrightText: 2026 Froststrap
..
.. SPDX-License-Identifier: MPL-2.0

TODO
====

Mostly just listing out notes for later on what to do.

Stuff for ui-redesign branch to be considered finished
------------------------------------------------------

Important
~~~~~~~~~

Must be done for to be mergable.

- Seperation of UI into Froststrap.UI root directory

In current- if you only need to change specifically UI, due to incremental builds not being currently heavily supported by us
it may take seconds to a minute to be able to see that change with the C# re-compilation.

The sugguestion is get the current mainstream UI to be seperating into this new system which allows immediate improvements and speedier
compile times whilst working on development of the UI.

The idea in practice in the codebase is UI obviously the only holder of UI plugins/libraries like Avalonia, and CommunityMVVM.
This means that currently with Avalonia app construction takes place in AppBuilder with Froststrap's Program.cs, needs to move out.

Whilst prepping the move, before moving from FluentAvalonia- move it inside of the folder so we have a valid idea on the root structure
before actually making the move with the deletion of that sub-project. Basically so throughout the whole move from FluentAvalonia, no churn in the root
dir except for the files to kick off the new Froststrap.UI directory.

- Validation that all the AXAML definitions are in a state where another more practical element could be used

Practical in this sense means, another element builtin doesn't do a whole bunch of us-managed plumbing work.
Another thing to mention is with the AXAML tree being really big for no reason, like a shortcut like having a panel, holding panels with panels
when could just be 1 big grid and define it how you want, following K.I.S.S. principle.

What needs to be figured out
~~~~~~~~~~~~~~~~~~~~~~~~~~~~

- New structure of file and directories for classification

Current solution is 200% less than sub-par.

The fact we have a Dialogs directory, but none for windows or other nice-ities,
makes it hard for someone new to get into the project and help maintain it fast.

- UI design

Obviously- to create a UI via IL, you want to have a figure of new UI layout to be able to build with to make a lot of guess work go away.

Known warnings which are worth fixing
-------------------------------------

Sorted top-to-bottom on try to fix first

When fixing these, realization of hard ones to solve,
you can switch around priority

- CA1863
- CA5392
- CA1852
- CA1812
- CA2000
- CA1416

