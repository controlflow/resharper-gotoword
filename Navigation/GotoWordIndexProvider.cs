using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.Application.Env;
using JetBrains.Application.Threading;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.ControlFlow.GoToWord.Hacks;
using JetBrains.ReSharper.Feature.Services.Goto;
using JetBrains.ReSharper.Feature.Services.Occurences;
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

    [NotNull] private static readonly Key<List<IOccurence>> TextualOccurances = new Key<List<IOccurence>>("TextualOccurances");
    [NotNull] private static readonly Key<object> FirstTimeLookup = new Key<object>("FirstTimeLookup");

    public IEnumerable<MatchingInfo> FindMatchingInfos(
      IdentifierMatcher matcher, INavigationScope scope,
      GotoContext gotoContext, CheckForInterrupt checkCancelled)
    {
      var solution = scope.GetSolution();
      if (solution == null) return EmptyList<MatchingInfo>.InstanceList;

      var filterText = matcher.Filter;

      var wordIndex = solution.GetPsiServices().WordIndex;
      var wordCache = (ICache) wordIndex;
      var words = wordIndex
        .GetWords(filterText)
        .OrderByDescending(word => word.Length).FirstOrDefault();

      if (gotoContext.GetData(FirstTimeLookup) == null)
      {
        // force word index to process all not processed files
        if (PrepareSolutionFiles(solution, wordCache, checkCancelled))
        {
          gotoContext.PutData(FirstTimeLookup, FirstTimeLookup);
        }
      }

      if (words == null) return EmptyList<MatchingInfo>.InstanceList;

      var sourceFiles = new HashSet<IPsiSourceFile>();
      sourceFiles.AddRange(wordIndex.GetFilesContainingWord(words));
      sourceFiles.AddRange(wordIndex.GetFilesContainingWord(filterText));

      var occurences = new List<IOccurence>();

      var caseInsensitive = (scope.ExtendedSearchFlag == LibrariesFlag.SolutionOnly);
      if (!caseInsensitive)
      {
        foreach (var sourceFile in sourceFiles)
        {
          var buffer = sourceFile.Document.Buffer;
          if (buffer == null) continue;

          if (sourceFile.Properties.IsNonUserFile) continue;

          for (var index = 0; (index = buffer.IndexOf(filterText, index)) != -1; index++)
          {
            var range = TextRange.FromLength(index, filterText.Length);
            var occurence = new RangeOccurence(
              sourceFile, new DocumentRange(sourceFile.Document, range), OccurenceType.TextualOccurence,
              new OccurencePresentationOptions());
            occurences.Add(occurence);

            if (checkCancelled()) break;
          }

          if (checkCancelled()) break;
        }
      }
      else
      {
        foreach (var sourceFile in sourceFiles)
        {
          var text = sourceFile.Document.GetText();
          if (text == null) continue;

          if (sourceFile.Properties.IsNonUserFile) continue;

          for (var index = 0; (index = text.IndexOf(
            filterText, index, StringComparison.OrdinalIgnoreCase)) != -1; index++)
          {
            var range = TextRange.FromLength(index, filterText.Length);
            var occurence = new RangeOccurence(sourceFile, new DocumentRange(sourceFile.Document, range));
            occurences.Add(occurence);

            if (checkCancelled()) break;
          }
      
          if (checkCancelled()) break;
        }
      }

      if (occurences.Count > 0)
      {
        gotoContext.PutData(TextualOccurances, occurences);
        return new[]
          {
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

    private static bool PrepareSolutionFiles(
      [NotNull] ISolution solution, [NotNull] ICache wordIndex,
      [NotNull] CheckForInterrupt checkCancelled)
    {
      var locks = solution.GetComponent<IShellLocks>();
      var configurations = solution.GetComponent<RunsProducts.ProductConfigurations>();
      var persistentIndexManager = solution.GetComponent<IPersistentIndexManager>();
      var psiModules = solution.GetComponent<IPsiModules>();

      using (var pool = new MultiCoreFibersPool("Updating word index cache", locks, configurations))
      using (var fibers = pool.Create("Updating word index cache for 'go to word' navigation"))
      {
        foreach (var project in solution.GetAllProjects())
        foreach (var module in psiModules.GetPsiModules(project))
        {
          foreach (var psiSourceFile in module.SourceFiles)
          {
            // workaround WordIndex2 implementation, to force indexing
            // unknown project file types like *.txt files and other

            var fileToCheck = psiSourceFile.Properties.ShouldBuildPsi
              ? psiSourceFile
              : new SourceFileToBuildCache(psiSourceFile);

            if (!wordIndex.UpToDate(fileToCheck))
            {
              var sourceFile = psiSourceFile;
              fibers.EnqueueJob(() =>
              {
                var data = wordIndex.Build(sourceFile, false);
                wordIndex.Merge(sourceFile, data);
                persistentIndexManager.OnPersistentCachesUpdated(sourceFile);
              });
            }

            if (checkCancelled()) return false;
          }
        }
      }

      return true;
    }
  }
}