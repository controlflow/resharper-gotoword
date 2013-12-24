ReSharper Go to Word plugin
---------------------------

#### Features

* Search whole solution for any text in any types of project files with triple `Ctrl`+`T`:
![navigation](/Content/navigation.png)
* Really fast case insensitive search, powered by ReSharper built-in caches
* Can search for currently selected text
* ReSharper's "Find results" tool window with powerful grouping and previews
![occurances](/Content/occurances.png)

#### In action

![demo](/Content/gotoword.gif)

#### Installation

This plugin is available to download in ReSharper 8 [extensions gallery](https://resharper-plugins.jetbrains.com/packages/ReSharper.GoToWord):
![extensions](/Content/manager.png)

#### Known issues

* By now, ReSharper plugins are not able to register hotkeys after installation (because of VS
integration issues). You can assign keyboard shortcut manually by using VisualStudio's *Options* -
*Environment* - *Keyboard* dialog, just find `ReSharper_GotoWordIndex` action there.

![hotkeys](/Content/hotkeys.png)

* Users reporting that in some VS versions they are loosing hotkey for plugin every VS restart.
Currently there is no way to workaround this, so you can only use plugin from menu *ReSharper* -
*Navigate* - *Go to Word...* or triple `Ctrl`+`T` hotkey.

* This plugin highly relays on infrastructure changes introduced in ReSharper v8.0 (universal
'word index' cache with language-independent tokenization), so it cannot be easily backported
to 7.x or earlier R# versions.