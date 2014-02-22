using System;
using System.Linq;
using System.Collections.Generic;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.ControlFlow.GoToWord.Hacks;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Caches;
using JetBrains.ReSharper.Psi.Modules;
using JetBrains.Text;
using JetBrains.Util;
using JetBrains.DocumentModel;

#if RESHARPER8
using JetBrains.Application;
using JetBrains.Application.Env;
using JetBrains.Application.Threading;
using JetBrains.ReSharper.Feature.Services.Search;
using JetBrains.ReSharper.Feature.Services.Navigation.Occurences;
using JetBrains.ReSharper.Feature.Services.Goto;
#elif RESHARPER81
using JetBrains.DataFlow;
using JetBrains.Application.Threading.Tasks;
using JetBrains.ReSharper.Feature.Services.Goto;
using JetBrains.ReSharper.Feature.Services.Navigation.Occurences;
using JetBrains.ReSharper.Feature.Services.Navigation.Search;
// NOTE: THANKS UNIVERSE C# CAN DO THAT:
using CheckForInterrupt = System.Func<bool>;
#elif RESHARPER9
using JetBrains.DataFlow;
using JetBrains.Application.ComponentModel;
using JetBrains.Application.Threading.Tasks;
using JetBrains.ReSharper.Feature.Services.Navigation;
using JetBrains.ReSharper.Feature.Services.Navigation.Goto.Misc;
using JetBrains.ReSharper.Feature.Services.Navigation.Goto.ProvidersAPI;
using JetBrains.ReSharper.Feature.Services.Occurences;
using CheckForInterrupt = System.Func<bool>;
#endif

namespace JetBrains.ReSharper.ControlFlow.GoToWord
{
  [ShellFeaturePart]
  public sealed class GotoWordIndexProvider : IGotoWordIndexProvider
  {
    [NotNull] private readonly IShellLocks myShellLocks;

#if RESHARPER8
    [NotNull] private readonly RunsProducts.ProductConfigurations myConfigurations;
    const string GoToWordPoolName = "Go to Word search pool";

    public GotoWordIndexProvider(
      [NotNull] RunsProducts.ProductConfigurations configurations, [NotNull] IShellLocks shellLocks)
    {
      myConfigurations = configurations;
      myShellLocks = shellLocks;
    }
#elif RESHARPER81 || RESHARPER9
    [NotNull] private readonly Lifetime myLifetime;
    [NotNull] private readonly ITaskHost myTaskHost;

    public GotoWordIndexProvider(
      [NotNull] Lifetime lifetime, [NotNull] ITaskHost taskHost, [NotNull] IShellLocks shellLocks)
    {
      myLifetime = lifetime;
      myTaskHost = taskHost;
      myShellLocks = shellLocks;
    }
#endif

    public bool IsApplicable(
      [NotNull] INavigationScope scope, [NotNull] GotoContext gotoContext, [NotNull] IdentifierMatcher matcher)
    {
      return true;
    }

    [NotNull] public IEnumerable<ChainedNavigationItemData> GetNextChainedScopes(
      [NotNull] GotoContext gotoContext, [NotNull] IdentifierMatcher matcher,
      [NotNull] INavigationScope containingScope, [NotNull] CheckForInterrupt checkForInterrupt)
    {
      return EmptyList<ChainedNavigationItemData>.InstanceList;
    }

    [NotNull] private static readonly Key<List<IOccurence>> GoToWordOccurrences =
      new Key<List<IOccurence>>("GoToWordOccurrences");

    [NotNull] private static readonly Key<object> GoToWordFirstTimeLookup =
      new Key<object>("GoToWordFirstTimeLookup");

    [NotNull] public IEnumerable<MatchingInfo> FindMatchingInfos(
      [NotNull] IdentifierMatcher matcher, [NotNull] INavigationScope scope,
      [NotNull] GotoContext gotoContext, [NotNull] CheckForInterrupt checkCanceled)
    {
      var solution = scope.GetSolution();
      if (solution == null) return EmptyList<MatchingInfo>.InstanceList;

      var filterText = matcher.Filter;
      var occurrences = new List<IOccurence>();

      myShellLocks.AssertReadAccessAllowed();

      if (scope.ExtendedSearchFlag == LibrariesFlag.SolutionOnly)
      {
        FindByWords(filterText, solution, occurrences, gotoContext, checkCanceled);
      }
      else
      {
        FindTextual(filterText, solution, occurrences, checkCanceled);
      }

      if (occurrences.Count > 0)
      {
        gotoContext.PutData(GoToWordOccurrences, occurrences);
        return new[] {new MatchingInfo(filterText, EmptyList<IdentifierMatch>.InstanceList)};
      }

      return EmptyList<MatchingInfo>.InstanceList;
    }

    private void FindByWords(
      [NotNull] string textToSearch, [NotNull] ISolution solution, [NotNull] List<IOccurence> occurrences,
      [NotNull] UserDataHolder gotoContext, [NotNull] CheckForInterrupt checkCanceled)
    {
      var wordIndex = solution.GetPsiServices().WordIndex;
      var longestWord = wordIndex.GetWords(textToSearch)
        .OrderByDescending(word => word.Length).FirstOrDefault();

      if (gotoContext.GetData(GoToWordFirstTimeLookup) == null)
      {
        // force word index to process all not processed files
        if (PrepareWordIndex(solution, wordIndex, checkCanceled))
        {
          gotoContext.PutData(GoToWordFirstTimeLookup, GoToWordFirstTimeLookup);
        }
      }

      if (longestWord == null) return;

      var sourceFiles = new HashSet<IPsiSourceFile>();
      sourceFiles.AddRange(wordIndex.GetFilesContainingWord(longestWord));
      sourceFiles.AddRange(wordIndex.GetFilesContainingWord(textToSearch));

      foreach (var sourceFile in sourceFiles)
      {
        if (IsFilteredFile(sourceFile)) continue;
        SearchInFile(textToSearch, sourceFile, occurrences, checkCanceled);
      }
    }

    private void FindTextual(
      [NotNull] string searchText, [NotNull] ISolution solution,
      [NotNull] List<IOccurence> consumer, [NotNull] CheckForInterrupt checkCanceled)
    {
#if RESHARPER8
      using (var pool = new MultiCoreFibersPool(GoToWordPoolName, myShellLocks, myConfigurations))
      using (var fibers = pool.Create("Files scan for textual occurances"))
#elif RESHARPER81 || RESHARPER9
      using (var fibers = myTaskHost.CreateBarrier(
        myLifetime, checkCanceled, sync: false, takeReadLock: false))
#endif
      {
        foreach (var psiSourceFile in GetAllSolutionFiles(solution))
        {
          if (IsFilteredFile(psiSourceFile)) continue;

          var sourceFile = psiSourceFile;
          fibers.EnqueueJob(() =>
          {
            using (ReadLockCookie.Create())
            {
              SearchInFile(searchText, sourceFile, consumer, checkCanceled);
            }
          });

          if (checkCanceled()) return;
        }
      }
    }

    private static bool IsFilteredFile([NotNull] IPsiSourceFile sourceFile)
    {
      // filter out files from 'misc files project'
      var projectFile = sourceFile.ToProjectFile();
      if (projectFile == null) return true;

      var project = projectFile.ParentFolder as IProject;
      if (project != null)
      {
        // do not include .csproj in search (unable to open in VS without unloading)
        if (projectFile.LanguageType.Is<MSBuildProjectFileType>())
        {
          if (project.IsOpened) return true;
        }
      }

      return false;
    }

    private static void SearchInFile(
      [NotNull] string searchText, [NotNull] IPsiSourceFile sourceFile,
      [NotNull] List<IOccurence> consumer, [NotNull] CheckForInterrupt checkCanceled)
    {
      var fileText = sourceFile.Document.GetText();
      if (fileText == null) return;

      var index = 0;
      while ((index = fileText.IndexOf(searchText, index, StringComparison.OrdinalIgnoreCase)) >= 0)
      {
        var occurrenceRange = TextRange.FromLength(index, searchText.Length);
        var documentRange = new DocumentRange(sourceFile.Document, occurrenceRange);
        var occurrence = new RangeOccurence(sourceFile, documentRange);

        lock (consumer)
        {
          consumer.Add(occurrence);
        }

        if (checkCanceled()) break;
        index++;
      }
    }

    public IEnumerable<IOccurence> GetOccurencesByMatchingInfo(
      [NotNull] MatchingInfo navigationInfo, [NotNull] INavigationScope scope,
      [NotNull] GotoContext gotoContext, [NotNull] CheckForInterrupt checkForInterrupt)
    {
      var occurrences = gotoContext.GetData(GoToWordOccurrences);
      return occurrences ?? EmptyList<IOccurence>.InstanceList;
    }

    private bool PrepareWordIndex(
      [NotNull] ISolution solution, [NotNull] IWordIndex wordIndex, [NotNull] CheckForInterrupt checkCanceled)
    {
      var persistentIndexManager = solution.GetComponent<IPersistentIndexManager>();

#if RESHARPER8
      using (var pool = new MultiCoreFibersPool(GoToWordPoolName, myShellLocks, myConfigurations))
      using (var fibers = pool.Create("Updating word index cache"))
#elif RESHARPER81 || RESHARPER9
      using (var fibers = myTaskHost.CreateBarrier(
        myLifetime, checkCanceled, sync: false, takeReadLock: false))
#endif
      {
        var wordCache = (ICache) wordIndex;
        foreach (var psiSourceFile in GetAllSolutionFiles(solution))
        {
          // workaround WordIndex2 implementation, to force indexing
          // unknown project file types like *.txt files and other
          var fileToCheck = psiSourceFile.Properties.ShouldBuildPsi
            ? psiSourceFile : new SourceFileToBuildCache(psiSourceFile);

          if (!wordCache.UpToDate(fileToCheck))
          {
            var sourceFile = psiSourceFile;
            fibers.EnqueueJob(() =>
            {
              using (ReadLockCookie.Create())
              {
                var data = wordCache.Build(sourceFile, false);
                wordCache.Merge(sourceFile, data);

                persistentIndexManager.OnPersistentCachesUpdated(sourceFile);
              }
            });
          }

          if (checkCanceled()) return false;
        }
      }

      return true;
    }

    [NotNull]
    private static IEnumerable<IPsiSourceFile> GetAllSolutionFiles([NotNull] ISolution solution)
    {
      var psiModules = solution.GetComponent<IPsiModules>();

      foreach (var project in solution.GetAllProjects())
      {
        var projectKind = project.ProjectProperties.ProjectKind;
        if (projectKind == ProjectKind.MISC_FILES_PROJECT) continue;

        foreach (var module in psiModules.GetPsiModules(project))
        foreach (var sourceFile in module.SourceFiles)
        {
          // filter out synthetic files out of solution
          var projectFile = sourceFile.ToProjectFile();
          if (projectFile == null) continue;

          yield return sourceFile;
        }
      }
    }
  }
}