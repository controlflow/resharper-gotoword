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
using JetBrains.ReSharper.GoToWord.Navigation.Presentation;
using JetBrains.UI.GotoByName;

namespace JetBrains.ReSharper.GoToWord
{
  public class GotoWordIndexController : GotoControllerBase<IGotoWordIndexProvider, IChainedSearchProvider, IInstantGotoProvider>
  {
    public GotoWordIndexController(
      [NotNull] Lifetime lifetime, [NotNull] ISolution solution, LibrariesFlag librariesFlag,
      [NotNull] IShellLocks locks, [NotNull] ITaskHost taskHost, [NotNull] GotoByNameModel model)
      : base(lifetime, solution, solution, librariesFlag, locks, taskHost, false, model)
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