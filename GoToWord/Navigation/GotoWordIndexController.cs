using JetBrains.ReSharper.Feature.Services.Navigation.Goto.ProvidersAPI.ChainedProviders;
using System.Collections.Generic;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.DataFlow;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Navigation.Goto.Controllers;
using JetBrains.ReSharper.Feature.Services.Navigation.Goto.ProvidersAPI;
using JetBrains.ReSharper.Feature.Services.Navigation.Goto.Misc;
using JetBrains.Application.Threading.Tasks;

namespace JetBrains.ReSharper.GoToWord
{
  public class GotoWordIndexController
    : GotoControllerBase<IGotoWordIndexProvider, IChainedSearchProvider, IGotoWordIndexProvider>
  {
    public GotoWordIndexController([NotNull] Lifetime lifetime, [NotNull] ISolution solution,
      LibrariesFlag librariesFlag, [NotNull] IShellLocks locks, [NotNull] ITaskHost taskHost)
      : base(lifetime, solution, solution, librariesFlag, locks, taskHost, enableMulticore: false)
    {
      var manager = GotoByNameModelManager.GetInstance(solution);
      manager.ProcessModel<GotoWordModelInitializer>(Model, lifetime);
    }

    protected override ICollection<ChainedNavigationItemData> InitScopes(bool isSearchingInLibs)
    {
      

      var scope = new SolutionNavigationScope(ScopeData as ISolution, isSearchingInLibs);
      return new[] { new ChainedNavigationItemData(null, scope) };
    }
  }
}