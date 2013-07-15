ReSharper Go to Word plugin
---------------------------

This plugin allows you to utilize the power of R# caches and occurances browsing
UI capabilities (with various groupings and previews) for simple textual search.

#### Features

![occurances](/Content/occurances.png)

#### Installation

This plugin is available in ReSharper 8 Extension Gallery:

TODO: pic

#### Known issues

* By now, ReSharper plugins are not able to register hotkeys after installation
(because of VS integration issues). You can assign keyboard shortcut manually
by using VisualStudio's *Options* - *Environment* - *Keyboard* dialog,
just find *"ReSharper_GotoWordIndex"* action.

![hotkeys](/Content/hotkeys.png)

* This plugin highly relays on infrastructure changes introduced in ReSharper 8.0
(universal word index cache for language-indendent tokenization), so it will not
be backported to 7.x or earlier R# versions.