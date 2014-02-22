using System.Collections.Generic;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.DataFlow;
using JetBrains.ProjectModel;

#if RESHARPER8
using JetBrains.ReSharper.Feature.Services.Search;
#elif RESHARPER81
using JetBrains.ReSharper.Feature.Services.Navigation.Search;
using JetBrains.Application.Threading.Tasks;
#endif
#if RESHARPER8 || RESHARPER81
using JetBrains.ReSharper.Feature.Services.Goto;
using JetBrains.ReSharper.Features.Common.GoToByName;
using JetBrains.ReSharper.Features.Common.GoToByName.Controllers;
#elif RESHARPER9
using JetBrains.ReSharper.Feature.Services.Navigation.Goto.Controllers;
using JetBrains.ReSharper.Feature.Services.Navigation.Goto.ProvidersAPI;
using JetBrains.ReSharper.Feature.Services.Navigation.Goto.Misc;
using JetBrains.Application.Threading.Tasks;
#endif

namespace JetBrains.ReSharper.ControlFlow.GoToWord
{
  public class GotoWordIndexController
    : GotoControllerBase<IGotoWordIndexProvider, IGotoWordIndexProvider>
  {
#if RESHARPER8
    public GotoWordIndexController(
      [NotNull] Lifetime lifetime, [NotNull] ISolution solution,
      LibrariesFlag librariesFlag, [NotNull] IShellLocks locks)
      : base(lifetime, solution, solution, librariesFlag, locks, enableMulticore: false)
#elif RESHARPER81 || RESHARPER9
    public GotoWordIndexController([NotNull] Lifetime lifetime, [NotNull] ISolution solution,
      LibrariesFlag librariesFlag, [NotNull] IShellLocks locks, [NotNull] ITaskHost taskHost)
      : base(lifetime, solution, solution, librariesFlag, locks, taskHost, enableMulticore: false)
#endif
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