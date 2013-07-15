using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.ProjectModel;
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
      yield break;
    }

    [NotNull] private static readonly Key<List<IOccurence>>
      TextualOccurances = new Key<List<IOccurence>>("TextualOccurances");

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

      var mm = solution.GetComponent<IPsiModules>();
      var pim = solution.GetComponent<IPersistentIndexManager>();
      foreach (var project in scope.GetSolution().GetAllProjects())
      {
        foreach (var psiModule in mm.GetPsiModules(project))
        {
          foreach (var psiSourceFile in psiModule.SourceFiles)
          {
            if (!wordCache.UpToDate(psiSourceFile))
            {
              var data = wordCache.Build(psiSourceFile, false);
              wordCache.Merge(psiSourceFile, data);
              pim.OnPersistentCachesUpdated(psiSourceFile);
            }
          }
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
  }
}