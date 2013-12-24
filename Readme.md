ReSharper Go to Word plugin
---------------------------

#### Features

* Search whole solution for any text in any types of project files
![navigation](/Content/navigation.png)
* Really fast case insensitive search, powered by ReSharper caches
* Standard ReSharper "find results" dialog with grouping and previews
![occurances](/Content/occurances.png)
* Integrates with ReSharper 8 integrates (triple `Ctrl`+`C` press)

#### Installation

This plugin is available in ReSharper Extension Manager gallery:
![occurances](/Content/manager.png)

#### Known issues

* By now, ReSharper plugins are not able to register hotkeys after installation
(because of VS integration issues). You can assign keyboard shortcut manually
by using VisualStudio's *Options* - *Environment* - *Keyboard* dialog,
just find *"ReSharper_GotoWordIndex"* action.

![hotkeys](/Content/hotkeys.png)

* Users reporting that shortcuts are unabled 

* This plugin highly relays on infrastructure changes introduced in ReSharper v8.0
(universal 'word index' cache with language-independent tokenization),
so it cannot be easily backported to 7.x or earlier R# versions.