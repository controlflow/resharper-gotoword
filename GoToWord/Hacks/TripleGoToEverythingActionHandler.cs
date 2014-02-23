using JetBrains.ActionManagement;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.Application.DataContext;
using JetBrains.DataFlow;
using JetBrains.Util.Logging;
using DataConstants = JetBrains.ProjectModel.DataContext.DataConstants;

#if RESHARPER8 || RESHARPER81
using JetBrains.ReSharper.Features.Common.GoToByName.Controllers;
using JetBrains.ReSharper.Features.Finding.GoToType;
#elif RESHARPER9
using IActionHandler = JetBrains.UI.ActionsRevised.IExecutableAction;
using JetBrains.ReSharper.Feature.Services.Navigation.Goto.Controllers;
using JetBrains.ReSharper.Features.Navigation.Features.Goto.GoToType;
#endif

namespace JetBrains.ReSharper.GoToWord.Hacks
{
  [ShellComponent]
  public class TripleGoToEverythingActionHandler : IActionHandler
  {
    [NotNull] private readonly IActionManager myActionManager;

    public TripleGoToEverythingActionHandler([NotNull] IActionManager actionManager, Lifetime lifetime)
    {
      myActionManager = actionManager;

      const string gotoTypeActionId = "GotoType";

#if RESHARPER8 || RESHARPER81
      var gotoTypeAction = actionManager.TryGetAction(gotoTypeActionId) as IUpdatableAction;
      if (gotoTypeAction != null)
      {
        gotoTypeAction.AddHandler(lifetime, this);
      }
#elif RESHARPER9
      var gotoTypeAction = actionManager.Defs.TryGetActionDefById(gotoTypeActionId);
      if (gotoTypeAction != null)
      {
        lifetime.AddAction(() => actionManager.Handlers.RemoveHandler(gotoTypeAction, this));
        actionManager.Handlers.AddHandler(gotoTypeAction, this);
      }
#endif
    }

    public bool Update(IDataContext context, ActionPresentation presentation, DelegateUpdate nextUpdate)
    {
      return context.CheckAllNotNull(DataConstants.SOLUTION);
    }

    public void Execute(IDataContext context, DelegateExecute nextExecute)
    {
      // if current controller is GoToType (second press after GoToEverything, or GoToEverything disabled)
      var controller = context.GetData(GotoTypeAction.GotoController);
      if (controller is GotoDeclaredElementController)
      {
#if RESHARPER8 || RESHARPER81
        var gotoWordAction = myActionManager.TryGetAction(GotoWordIndexAction.Id) as IExecutableAction;
        if (gotoWordAction != null)
        {
          gotoWordAction.Execute(context);
          return;
        }
#elif RESHARPER9
        var gotoWordAction = myActionManager.Defs.TryGetActionDefById(GotoWordIndexAction.Id);
        if (gotoWordAction != null)
        {
          var evaluatedAction = myActionManager.Handlers.Evaluate(gotoWordAction, context);
          if (evaluatedAction.IsAvailable) evaluatedAction.Execute();
          return;
        }
#endif

        Logger.LogError("Action {0} is not found!", GotoWordIndexAction.Id);
      }

      nextExecute();
    }
  }
}