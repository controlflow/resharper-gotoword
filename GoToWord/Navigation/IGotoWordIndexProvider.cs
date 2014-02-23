#if RESHARPER8 || RESHARPER81
using JetBrains.ReSharper.Feature.Services.Goto;
using JetBrains.ReSharper.Feature.Services.Goto.ChainedProviders;
#elif RESHARPER9
using JetBrains.ReSharper.Feature.Services.Navigation.Goto.ProvidersAPI;
using JetBrains.ReSharper.Feature.Services.Navigation.Goto.ProvidersAPI.ChainedProviders;
#endif

namespace JetBrains.ReSharper.GoToWord
{
  public interface IGotoWordIndexProvider
    : IOccurenceNavigationProvider, IChainedSearchProvider { }
}