using JetBrains.ActionManagement;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.Application.DataContext;
using JetBrains.DataFlow;
using JetBrains.Util.Logging;

#if RESHARPER8 || RESHARPER81
using JetBrains.ReSharper.Features.Common.GoToByName.Controllers;
using JetBrains.ReSharper.Features.Finding.GoToType;
#elif RESHARPER9
using IActionHandler = JetBrains.UI.ActionsRevised.IAction;
using JetBrains.ReSharper.Feature.Services.Navigation.Goto.Controllers;
using JetBrains.ReSharper.Features.Navigation.Features.Goto.GoToType;
#endif

namespace JetBrains.ReSharper.ControlFlow.GoToWord.Hacks
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
        actionManager.Handlers.AddHandler(gotoTypeAction, this);
      }
#endif
    }

    public bool Update(IDataContext context, ActionPresentation presentation, DelegateUpdate nextUpdate)
    {
      return nextUpdate();
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
          myActionManager.Handlers.Evaluate(gotoWordAction, context);
        }
#endif

        Logger.LogError("Action {0} is not found!", GotoWordIndexAction.Id);
      }

      nextExecute();
    }
  }
}