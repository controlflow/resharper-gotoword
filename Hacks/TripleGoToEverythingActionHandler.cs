using JetBrains.ActionManagement;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.Application.DataContext;
using JetBrains.DataFlow;
using JetBrains.ReSharper.Features.Common.GoToByName.Controllers;
using JetBrains.ReSharper.Features.Finding.GoToType;

namespace JetBrains.ReSharper.ControlFlow.GoToWord.Hacks
{
  [ShellComponent]
  public class TripleGoToEverythingActionHandler : IActionHandler
  {
    [NotNull] private readonly IActionManager myActionManager;

    public TripleGoToEverythingActionHandler([NotNull] IActionManager manager, Lifetime lifetime)
    {
      myActionManager = manager;

      const string gotoTypeActionId = "GotoType";
      var gotoTypeAction = manager.TryGetAction(gotoTypeActionId) as IUpdatableAction;
      if (gotoTypeAction != null)
      {
        gotoTypeAction.AddHandler(lifetime, this);
      }
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
        var goToWordAction = myActionManager.TryGetAction(GotoWordIndexAction.Id) as IExecutableAction;
        if (goToWordAction != null)
        {
          goToWordAction.Execute(context);
          return;
        }
      }

      nextExecute();
    }
  }
}