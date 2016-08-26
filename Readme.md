ReSharper Go to Word plugin
---------------------------

### Status

I'm glad to announce that 'Go to Word' plugin was [reborn as a built-in 'Go to Text' feature in ReSharper 2016.2](https://blog.jetbrains.com/dotnet/2016/08/18/resharper-ultimate-2016-2-is-here/). It provides functionality, similar to original 'Go to Word' plugin, but supports searching for arbitrary strings (not just "words"). This plugin is no longer supported.

![gototext](https://d3nmt5vlzunoa1.cloudfront.net/dotnet/files/2016/08/find_text.png)

#### Features

* Search whole solution for any text in any types of project files with triple `Ctrl`+`T`:
![navigation](https://raw.githubusercontent.com/controlflow/resharper-gotoword/master/Content/navigation.png)
* Really fast case insensitive search, powered by ReSharper built-in caches
* Can search for currently selected text
* ReSharper's "Find results" tool window with powerful grouping and previews
![occurances](https://raw.githubusercontent.com/controlflow/resharper-gotoword/master/Content/occurances.png)

#### In action

![demo](https://raw.githubusercontent.com/controlflow/resharper-gotoword/master/Content/gotoword.gif)

#### Installation

This plugin is available to download in ReSharper 8 [extensions gallery](https://resharper-plugins.jetbrains.com/packages/ReSharper.GoToWord):
![extensions](https://raw.githubusercontent.com/controlflow/resharper-gotoword/master/Content/manager.png)

#### Known issues

* By now, ReSharper plugins are not able to register hotkeys after installation (because of VS
integration issues). You can assign keyboard shortcut manually by using VisualStudio's *Options* -
*Environment* - *Keyboard* dialog, just find `ReSharper_GotoWordIndex` action there.

![hotkeys](https://raw.githubusercontent.com/controlflow/resharper-gotoword/master/Content/hotkeys.png)

* Users reporting that in some VS versions they are loosing hotkey for plugin every VS restart.
Currently there is no way to workaround this, so you can only use plugin from menu *ReSharper* -
*Navigate* - *Go to Word...* or triple `Ctrl`+`T` hotkey.

* This plugin highly relays on infrastructure changes introduced in ReSharper v8.0 (universal
'word index' cache with language-independent tokenization), so it cannot be easily backported
to 7.x or earlier R# versions.
