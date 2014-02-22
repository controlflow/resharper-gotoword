using JetBrains.ReSharper.Feature.Services.Goto;
using JetBrains.ReSharper.Feature.Services.Goto.ChainedProviders;

namespace JetBrains.ReSharper.ControlFlow.GoToWord
{
  public interface IGotoWordIndexProvider
    : IOccurenceNavigationProvider, IChainedSearchProvider { }
}