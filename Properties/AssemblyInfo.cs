using System.Reflection;
using JetBrains.ActionManagement;
using JetBrains.Application.PluginSupport;

[assembly: AssemblyTitle("ReSharper.GoToWord")]
[assembly: AssemblyDescription("ReSharper 'Go to word' plugin")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Shvedov Alexander")]
[assembly: AssemblyProduct("ReSharper.GoToWord")]
[assembly: AssemblyCopyright("Copyright © Sjvedov Alexander, 2013")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: AssemblyVersion("0.9.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

#if RESHARPER7
[assembly: ActionsXml("ReSharper.GoToWord.R7.Actions.xml")]
#elif RESHARPER8
[assembly: ActionsXml("ReSharper.GoToWord.R8.Actions.xml")]
#endif

[assembly: PluginTitle("Go to word")]
[assembly: PluginDescription("Words navigation plugin for ReSharper")]
[assembly: PluginVendor("Shvedov Alexander")]
