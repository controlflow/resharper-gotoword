using System.Collections.Generic;
using JetBrains.ReSharper.Feature.Services.CodeCompletion;
using JetBrains.ReSharper.Feature.Services.CodeCompletion.Infrastructure;
using JetBrains.ReSharper.Feature.Services.Lookup;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.Util;

namespace JetBrains.ReSharper.GoToWord.CodeCompletion
{
  [Language(typeof(CSharpLanguage))]
  public class FileWordsCompletionProvider : ICodeCompletionItemsProvider
  {
    public object IsAvailable(ISpecificCodeCompletionContext context)
    {
      return true;
    }

    public TextLookupRanges GetDefaultRanges(ISpecificCodeCompletionContext context)
    {
      return new TextLookupRanges(TextRange.InvalidRange, TextRange.InvalidRange);
    }

    public void AddItemsGroups(
      ISpecificCodeCompletionContext context,
      IntellisenseManager intellisenseManager,
      GroupedItemsCollector collector, object data)
    {
      
    }

    public bool AddLookupItems(ISpecificCodeCompletionContext context, GroupedItemsCollector collector, object data)
    {
      collector.AddAtDefaultPlace(new TextLookupItem("Hello"));

      return false;
    }

    public void TransformItems(ISpecificCodeCompletionContext context, GroupedItemsCollector collector, object data)
    {
      
    }

    public void DecorateItems(ISpecificCodeCompletionContext context, GroupedItemsCollector collector, object data)
    {
      
    }

    public LookupFocusBehaviour GetLookupFocusBehaviour(ISpecificCodeCompletionContext context, object data)
    {
      return LookupFocusBehaviour.Hard;
    }

    public AutocompletionBehaviour GetAutocompletionBehaviour(ISpecificCodeCompletionContext context, object data)
    {
      return AutocompletionBehaviour.NoRecommendation;
    }

    public bool IsAvailableEx(IList<CodeCompletionType> codeCompletionTypes, ISpecificCodeCompletionContext context)
    {
      return true;
    }

    public bool IsEvaluationModeSupported(CodeCompletionParameters parameters)
    {
      return false;
    }

    public bool IsDynamic
    {
      get { return false; }
    }
  }
}
