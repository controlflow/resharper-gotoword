using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.DocumentModel;
using JetBrains.ReSharper.Feature.Services.Goto;
using JetBrains.ReSharper.Feature.Services.Navigation.Occurences;
using JetBrains.ReSharper.Feature.Services.Search;
using JetBrains.ReSharper.Psi;
using JetBrains.Text;
using JetBrains.Util;
using System.Linq;
#if RESHARPER7
using JetBrains.ReSharper.Psi.Caches;
using JetBrains.ReSharper.Feature.Services.Occurences;
#elif RESHARPER8
#endif

namespace JetBrains.ReSharper.ControlFlow.GoToWord
{
#if RESHARPER7
  [FeaturePart]
#elif RESHARPER8
  [SolutionFeaturePart]
#endif
  public sealed class GotoWordIndexProvider : IGotoWordIndexProvider
  {
#if RESHARPER7
    public bool IsApplicable(INavigationScope scope, GotoContext gotoContext)
    {
      return true;
    }
#elif RESHARPER8
    public bool IsApplicable(
      INavigationScope scope, GotoContext gotoContext, IdentifierMatcher matcher)
    {
      return true;
    }
#endif

    [NotNull] private static readonly Key<List<IOccurence>>
      TextualOccurances = new Key<List<IOccurence>>("TextualOccurances");

#if RESHARPER7
    public IEnumerable<MatchingInfo> FindMatchingInfos(
      IdentifierMatcher matcher, INavigationScope scope,
      CheckForInterrupt checkCancelled, GotoContext gotoContext)
#elif RESHARPER8
    public IEnumerable<MatchingInfo> FindMatchingInfos(
      IdentifierMatcher matcher, INavigationScope scope,
      GotoContext gotoContext, CheckForInterrupt checkCancelled)
#endif
    {
      var solution = scope.GetSolution();
      if (solution == null) return EmptyList<MatchingInfo>.InstanceList;

      var filterText = matcher.Filter;

#if RESHARPER7
      var wordIndex = solution.GetPsiServices().CacheManager.WordIndex;
      var words = wordIndex
        .GetWords(filterText, UniversalWordIndexProvider.Instance)
        .OrderByDescending(word => word.Length).FirstOrDefault();
#elif RESHARPER8
      var wordIndex = solution.GetPsiServices().WordIndex;
      var words = wordIndex
        .GetWords(filterText)
        .OrderByDescending(word => word.Length).FirstOrDefault();
#endif

      if (words == null) return EmptyList<MatchingInfo>.InstanceList;

      var sourceFiles = new HashSet<IPsiSourceFile>();
      sourceFiles.AddRange(wordIndex.GetFilesContainingWord(words));
      sourceFiles.AddRange(wordIndex.GetFilesContainingWord(filterText));

      var occurences = new List<IOccurence>();

      var caseInsensitive = (scope.ExtendedSearchFlag == LibrariesFlag.SolutionOnly);
      if (caseInsensitive)
      {
        foreach (var sourceFile in sourceFiles)
        {
          var projectFile = sourceFile.ToProjectFile();
          if (projectFile == null) continue;

          var buffer = sourceFile.Document.Buffer;
          if (buffer == null) continue;

          for (var index = 0; (index = buffer.IndexOf(filterText, index)) != -1; index++)
          {
            var range = TextRange.FromLength(index, filterText.Length);
#if RESHARPER7
            var occurence = new RangeOccurence(projectFile, range, OccurenceType.TextualOccurence);
#elif RESHARPER8
            var occurence = new RangeOccurence(sourceFile, new DocumentRange(sourceFile.Document, range));
#endif
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
          var projectFile = sourceFile.ToProjectFile();
          if (projectFile == null) continue;

          var text = sourceFile.Document.GetText();
          if (text == null) continue;

          for (var index = 0; (index = text.IndexOf(
            filterText, index, StringComparison.OrdinalIgnoreCase)) != -1; index++)
          {
            var range = TextRange.FromLength(index, filterText.Length);
#if RESHARPER7
            var occurence = new RangeOccurence(projectFile, range, OccurenceType.TextualOccurence);
#elif RESHARPER8
            var occurence = new RangeOccurence(sourceFile, new DocumentRange(sourceFile.Document, range));
#endif
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

#if RESHARPER7
    public IEnumerable<IOccurence> GetOccurencesByMatchingInfo(
      MatchingInfo navigationInfo, INavigationScope scope, GotoContext gotoContext)
#elif RESHARPER8
    public IEnumerable<IOccurence> GetOccurencesByMatchingInfo(
      MatchingInfo navigationInfo, INavigationScope scope,
      GotoContext gotoContext, CheckForInterrupt checkForInterrupt)
#endif
    {
      var occurences = gotoContext.GetData(TextualOccurances);
      return occurences ?? EmptyList<IOccurence>.InstanceList;
    }
  }
}