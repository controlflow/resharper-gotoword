using JetBrains.ReSharper.Feature.Services.Navigation.Goto.ProvidersAPI;
using JetBrains.ReSharper.Feature.Services.Navigation.Goto.ProvidersAPI.ChainedProviders;

namespace JetBrains.ReSharper.GoToWord
{
  public interface IGotoWordIndexProvider : IOccurenceNavigationProvider, IChainedSearchProvider { }
}