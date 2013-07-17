using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.Application.Env;
using JetBrains.Application.Threading;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.ControlFlow.GoToWord.Hacks;
using JetBrains.ReSharper.Feature.Services.Goto;
using JetBrains.ReSharper.Feature.Services.Search;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Caches;
using JetBrains.ReSharper.Psi.Modules;
using JetBrains.Text;
using JetBrains.Util;
using System.Linq;
using JetBrains.DocumentModel;
using JetBrains.ReSharper.Feature.Services.Navigation.Occurences;

namespace JetBrains.ReSharper.ControlFlow.GoToWord
{
  [ShellFeaturePart]
  public sealed class GotoWordIndexProvider : IGotoWordIndexProvider
  {
    public bool IsApplicable(
      INavigationScope scope, GotoContext gotoContext, IdentifierMatcher matcher)
    {
      return true;
    }

    public IEnumerable<ChainedNavigationItemData> GetNextChainedScopes(
      GotoContext gotoContext, IdentifierMatcher matcher,
      INavigationScope containingScope, CheckForInterrupt checkForInterrupt)
    {
      return EmptyList<ChainedNavigationItemData>.InstanceList;
    }

    [NotNull] private static readonly Key<List<IOccurence>>
      TextualOccurances = new Key<List<IOccurence>>("TextualOccurances");
    [NotNull] private static readonly Key<object>
      FirstTimeLookup = new Key<object>("FirstTimeLookup");

    public IEnumerable<MatchingInfo> FindMatchingInfos(
      IdentifierMatcher matcher, INavigationScope scope,
      GotoContext gotoContext, CheckForInterrupt checkCancelled)
    {
      var solution = scope.GetSolution();
      if (solution == null) return EmptyList<MatchingInfo>.InstanceList;

      // todo: look for ways to disable triming at start
      var filterText = matcher.Filter;
      var occurences = new List<IOccurence>();

      var findByWords = (scope.ExtendedSearchFlag == LibrariesFlag.SolutionOnly);
      if (findByWords)
      {
        var wordIndex = solution.GetPsiServices().WordIndex;
        var wordCache = (ICache) wordIndex;
        var words = wordIndex
          .GetWords(filterText)
          .OrderByDescending(word => word.Length)
          .FirstOrDefault();

        if (gotoContext.GetData(FirstTimeLookup) == null)
        {
          // force word index to process all not processed files
          if (PrepareWordIndex(solution, wordCache, checkCancelled))
            gotoContext.PutData(FirstTimeLookup, FirstTimeLookup);
        }

        if (words == null) return EmptyList<MatchingInfo>.InstanceList;

        var sourceFiles = new HashSet<IPsiSourceFile>();
        sourceFiles.AddRange(wordIndex.GetFilesContainingWord(words));
        sourceFiles.AddRange(wordIndex.GetFilesContainingWord(filterText));
        sourceFiles.RemoveWhere(sf => sf.ToProjectFile() == null);

        foreach (var sourceFile in sourceFiles)
        {
          var text = sourceFile.Document.GetText();
          if (text == null) continue;

          //if (sourceFile.Properties.IsNonUserFile) continue;
          // todo: filter d.ts

          for (var index = 0;
            (index = text.IndexOf(
              filterText, index, StringComparison.OrdinalIgnoreCase)) != -1;
            index++)
          {
            var range = TextRange.FromLength(index, filterText.Length);
            var occurence = new RangeOccurence(
              sourceFile, new DocumentRange(sourceFile.Document, range));

            occurences.Add(occurence);

            if (checkCancelled()) break;
          }

          if (checkCancelled()) break;
        }
      }
      else
      {
        FindTextual(filterText, solution, occurences, checkCancelled);
      }

      if (occurences.Count > 0)
      {
        gotoContext.PutData(TextualOccurances, occurences);
        return new[] {
          new MatchingInfo(filterText,
            EmptyList<IdentifierMatch>.InstanceList)
        };
      }

      return EmptyList<MatchingInfo>.InstanceList;
    }

    public IEnumerable<IOccurence> GetOccurencesByMatchingInfo(
      MatchingInfo navigationInfo, INavigationScope scope,
      GotoContext gotoContext, CheckForInterrupt checkForInterrupt)
    {
      var occurences = gotoContext.GetData(TextualOccurances);
      return occurences ?? EmptyList<IOccurence>.InstanceList;
    }

    private static bool PrepareWordIndex(
      [NotNull] ISolution solution, [NotNull] ICache wordIndex,
      [NotNull] CheckForInterrupt checkCancelled)
    {
      var locks = solution.GetComponent<IShellLocks>();
      var configurations = solution.GetComponent<RunsProducts.ProductConfigurations>();
      var persistentIndexManager = solution.GetComponent<IPersistentIndexManager>();

      using (var pool = new MultiCoreFibersPool("Updating word index cache", locks, configurations))
      using (var fibers = pool.Create("Updating word index cache for 'go to word' navigation"))
      {
        foreach (var psiSourceFile in GetAllSolutionFiles(solution))
        {
          // workaround WordIndex2 implementation, to force indexing
          // unknown project file types like *.txt files and other

          var fileToCheck = psiSourceFile.Properties.ShouldBuildPsi
            ? psiSourceFile
            : new SourceFileToBuildCache(psiSourceFile);

          if (!wordIndex.UpToDate(fileToCheck))
          {
            var sourceFile = psiSourceFile;
            fibers.EnqueueJob(() => {
              var data = wordIndex.Build(sourceFile, false);
              wordIndex.Merge(sourceFile, data);
              persistentIndexManager.OnPersistentCachesUpdated(sourceFile);
            });
          }

          if (checkCancelled()) return false;
        }
      }

      return true;
    }

    private static void FindTextual([NotNull] string filterText,
      [NotNull] ISolution solution, [NotNull] List<IOccurence> occurences,
      [NotNull] CheckForInterrupt checkCancelled)
    {
      var locks = solution.GetComponent<IShellLocks>();
      var configurations = solution.GetComponent<RunsProducts.ProductConfigurations>();

      using (var pool = new MultiCoreFibersPool("Textual search pool", locks, configurations))
      using (var fibers = pool.Create("Files scan for textual occurances"))
      {
        foreach (var psiSourceFile in GetAllSolutionFiles(solution))
        {
          // filter out syntetic files out of solution
          var projectFile = psiSourceFile.ToProjectFile();
          if (projectFile == null) continue;

          var sourceFile = psiSourceFile;
          fibers.EnqueueJob(() =>
          {
            var text = sourceFile.Document.GetText();
            if (text == null) return;

            for (var index = 0;
              (index = text.IndexOf(filterText, index, StringComparison.OrdinalIgnoreCase)) != -1;
              index++)
            {
              var range = TextRange.FromLength(index, filterText.Length);
              var occurence = new RangeOccurence(
                sourceFile, new DocumentRange(sourceFile.Document, range));
              lock (occurences)
              {
                occurences.Add(occurence);
              }

              if (checkCancelled()) break;
            }
          });

          if (checkCancelled()) return;
        }
      }
    }

    [NotNull]
    private static IEnumerable<IPsiSourceFile> GetAllSolutionFiles([NotNull] ISolution solution)
    {
      var psiModules = solution.GetComponent<IPsiModules>();

      foreach (var project in solution.GetAllProjects())
      {
        if (project.ProjectProperties.ProjectKind == ProjectKind.MISC_FILES_PROJECT)
          continue;

        foreach (var module in psiModules.GetPsiModules(project))
        {
          foreach (var sourceFile in module.SourceFiles)
          {
            // filter out syntetic files out of solution
            var projectFile = sourceFile.ToProjectFile();
            if (projectFile == null) continue;

            yield return sourceFile;
          }
        }
      }
    }
  }
}