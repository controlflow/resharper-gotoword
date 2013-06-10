using System.Collections.Generic;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.DataFlow;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Goto;
using JetBrains.ReSharper.Feature.Services.Goto.ChainedProviders;
using JetBrains.ReSharper.Feature.Services.Search;
using JetBrains.ReSharper.Features.Common.GoToByName;
using JetBrains.ReSharper.Features.Common.GoToByName.Controllers;

namespace JetBrains.ReSharper.ControlFlow.GoToWord
{
  public class GotoWordIndexController
    : GotoControllerBase<IGotoWordIndexProvider, IChainedProjectElementProvider>
  {
    public GotoWordIndexController(
      [NotNull] Lifetime lifetime, [NotNull] ISolution solution,
      LibrariesFlag librariesFlag, [NotNull] IShellLocks locks)
#if RESHARPER7
      : base(lifetime, solution, solution, librariesFlag, locks)
#elif RESHARPER8
      : base(lifetime, solution, solution, librariesFlag, locks, true)
#endif
    {
      var manager = GotoByNameModelManager.GetInstance(solution);
      manager.ProcessModel<GotoWordModelInitializer>(Model, lifetime);

      LibrariesFlagAutoSwitch = false;
    }

    protected override ICollection<ChainedNavigationItemData> InitScopes(bool isSearchingInLibs)
    {
      return new[] {
        new ChainedNavigationItemData(null,
          new SolutionNavigationScope(ScopeData as ISolution, isSearchingInLibs))
      };
    }
  }
}