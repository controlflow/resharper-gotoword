using JetBrains.ActionManagement;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.Application.DataContext;
using JetBrains.DataFlow;
using JetBrains.ReSharper.Feature.Services.Navigation.Goto.Controllers;
using JetBrains.Util.Logging;
using JetBrains.ReSharper.Features.Navigation.Features.Goto.GoToType;
using JetBrains.UI.ActionsRevised;

namespace JetBrains.ReSharper.GoToWord.Hacks
{
  [ShellComponent]
  public class TripleGoToEverythingActionHandler : IExecutableAction
  {
    [NotNull] private readonly IActionManager myActionManager;

    public TripleGoToEverythingActionHandler([NotNull] IActionManager actionManager, Lifetime lifetime)
    {
      myActionManager = actionManager;

      const string gotoTypeActionId = "GotoType";

      var gotoTypeAction = actionManager.Defs.TryGetActionDefById(gotoTypeActionId);
      if (gotoTypeAction != null)
      {
        lifetime.AddBracket(
          () => actionManager.Handlers.AddHandler(gotoTypeAction, this),
          () => actionManager.Handlers.RemoveHandler(gotoTypeAction, this));
      }
    }

    public bool Update(IDataContext context, ActionPresentation presentation, DelegateUpdate nextUpdate)
    {
      return context.CheckAllNotNull(ProjectModel.DataContext.DataConstants.SOLUTION);
    }

    public void Execute(IDataContext context, DelegateExecute nextExecute)
    {
      // if current controller is GoToType (second press after GoToEverything, or GoToEverything disabled)
      var controller = context.GetData(GotoTypeAction.GotoController);
      if (controller is GotoTypeController)
      {
        var gotoWordAction = myActionManager.Defs.TryGetActionDefById(GotoWordIndexAction.Id);
        if (gotoWordAction != null)
        {
          var evaluatedAction = myActionManager.Handlers.Evaluate(gotoWordAction, context);
          if (evaluatedAction.IsAvailable) evaluatedAction.Execute();

          return;
        }

        Logger.LogError("Action '{0}' is not found!", GotoWordIndexAction.Id);
      }

      nextExecute();
    }
  }
}