using System.Collections.Generic;
using System.Linq;
using JetBrains.ActionManagement;
using JetBrains.Application;
using JetBrains.Application.DataContext;
using JetBrains.Application.Progress;
using JetBrains.Application.Settings;
using JetBrains.DataFlow;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Goto;
using JetBrains.ReSharper.Feature.Services.Search;
using JetBrains.ReSharper.Features.Common.FindResultsBrowser;
using JetBrains.ReSharper.Features.Common.GoToByName.Controllers;
using JetBrains.Threading;
using JetBrains.UI.Application;
using JetBrains.UI.Application.Progress;
using JetBrains.UI.Controls.GotoByName;
using JetBrains.UI.GotoByName;
using JetBrains.UI.PopupWindowManager;
using JetBrains.UI.Theming;
using JetBrains.UI.Tooltips;
using JetBrains.Util;

namespace JetBrains.ReSharper.ControlFlow.GoToWord
{
  [ActionHandler("GotoWordIndex")]
  public class GotoWordIndexAction : IActionHandler
  {
    public bool Update(
      IDataContext context, ActionPresentation presentation, DelegateUpdate nextUpdate)
    {
      return true;
    }

    public void Execute(IDataContext context, DelegateExecute nextExecute)
    {
      var solution = context.GetData(ProjectModel.DataContext.DataConstants.SOLUTION);
      if (solution == null)
      {
        MessageBox.ShowError("Cannot execute the Go To action because there's no solution open.");
        return;
      }

      var instance = Shell.Instance;
      var locks = instance.GetComponent<IShellLocks>();

      Lifetimes.Define(solution.GetLifetime(), FAtomic: (definition, lifetime) =>
        {
          var controller = new GotoWordIndexController(
            lifetime, solution, LibrariesFlag.SolutionOnly, locks);

          EnableShowInFindResults(controller, definition);

#if RESHARPER7
          new GotoByNameMenu(definition, controller.Model,
                             instance.GetComponent<IThreading>(),
                             instance.GetComponent<ISettingsStore>(),
                             instance.GetComponent<ITooltipManager>(),
                             instance.GetComponent<UIApplication>().MainWindow,
                             instance.GetComponent<PopupWindowManager>(),
                             instance.GetComponent<WindowsMessageHookManager>(),
                             instance.GetComponent<MainWindowPopupWindowContext>(),
                             context.GetData(GotoByNameDataConstants.CurrentSearchText),
                             instance.GetComponent<IActionManager>(),
                             instance.GetComponent<IShortcutManager>(),
                             instance.GetComponent<ITheming>());

#elif RESHARPER8

          new GotoByNameMenu(
            instance.GetComponent<GotoByNameMenuComponent>(),
            definition, controller.Model,
            instance.GetComponent<UIApplication>().MainWindow,
            context.GetData(GotoByNameDataConstants.CurrentSearchText));
#endif



        });
    }

    private static void EnableShowInFindResults(
      GotoWordIndexController controller, LifetimeDefinition definition)
    {
      controller.FuncEtcItemExecute.Value = () =>
        Shell.Instance.Locks.ExecuteOrQueueReadLock("ShowInFindResults", () =>
      {
        var filterString = controller.Model.FilterText.Value;
        if (string.IsNullOrEmpty(filterString)) return;

        definition.Terminate();

        GotoWordBrowserDescriptor descriptor = null;
        var taskExecutor = Shell.Instance.GetComponent<UITaskExecutor>();
        if (!taskExecutor.FreeThreaded.ExecuteTask("Show Files In Find Results", TaskCancelable.Yes, indicator =>
        {
          indicator.TaskName = string.Format("Collecting words matching '{0}'", filterString);
          indicator.Start(1);

          List<Pair<IOccurence, MatchingInfo>> occurences;
          using (ReadLockCookie.Create())
          {
#if RESHARPER7
            occurences = controller.GetOccurencesToPresent(filterString, -1);
#elif RESHARPER8
            occurences = new List<Pair<IOccurence, MatchingInfo>>();
            controller.ConsumePresentableItems(filterString, -1, (items, behavior) =>
            {
              foreach (var item in items)
                occurences.Add(Pair.Of(item.Occurence, item.MatchingInfo));
            });
#endif
          }

          if (occurences.Any() && !indicator.IsCanceled)
            descriptor = new GotoWordBrowserDescriptor(
              controller.Solution, filterString, occurences.Select(x => x.First));

          indicator.Stop();
        }))
        {
          if (descriptor != null)
            descriptor.LifetimeDefinition.Terminate();
          return;
        }

        if (descriptor != null)
          FindResultsBrowser.ShowResults(descriptor);
      });
    }
  }
}
