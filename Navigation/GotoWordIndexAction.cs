using System.Collections.Generic;
using JetBrains.ActionManagement;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.Application.DataContext;
using JetBrains.Application.Progress;
using JetBrains.DataFlow;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Search;
using JetBrains.ReSharper.Features.Common.FindResultsBrowser;
using JetBrains.UI.Application;
using JetBrains.UI.Application.Progress;
using JetBrains.UI.Controls.GotoByName;
using JetBrains.UI.GotoByName;
using JetBrains.Util;
using DataConstants = JetBrains.TextControl.DataContext.DataConstants;

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

      Lifetimes.Define(
        lifetime: solution.GetLifetime(),
        FAtomic: (definition, lifetime) =>
        {
          var shell = Shell.Instance;
          var shellLocks = shell.GetComponent<IShellLocks>();
          var taskExecutor = shell.GetComponent<UITaskExecutor>();

          var controller = new GotoWordIndexController(
            definition.Lifetime, solution, LibrariesFlag.SolutionOnly, shellLocks);

          SetShowInFindResultsAction(controller, definition, shellLocks, taskExecutor);

          var gotoByNameMenu = shell.GetComponent<GotoByNameMenuComponent>();
          var uiApplication = shell.GetComponent<UIApplication>();
          var initialSearchText = context.GetData(GotoByNameDataConstants.CurrentSearchText);

          var textControl = context.GetData(DataConstants.TEXT_CONTROL);
          if (textControl != null)
          {
            var selection = textControl.Selection.Ranges.Value;
            if (selection != null && selection.Count == 1)
            {
              var docRange = selection[0].ToDocRangeNormalized();
              if (docRange.Length > 0)
              {
                var selectedText = textControl.Document.GetText(docRange);
                initialSearchText = new GotoByNameDataConstants.SearchTextData(
                  selectedText, TextRange.FromLength(selectedText.Length));
              }
            }
          }

          new GotoByNameMenu(
            gotoByNameMenu, definition, controller.Model,
            uiApplication.MainWindow, initialSearchText);
        });
    }

    private static void SetShowInFindResultsAction(
      [NotNull] GotoWordIndexController controller, [NotNull] LifetimeDefinition definition,
      [NotNull] IShellLocks shellLocks, [NotNull] UITaskExecutor taskExecutor)
    {
      controller.FuncEtcItemExecute.Value = () =>
        shellLocks.ExecuteOrQueueReadLock("ShowInFindResults", () =>
        {
          var filterString = controller.Model.FilterText.Value;
          if (string.IsNullOrEmpty(filterString)) return;

          definition.Terminate();

          GotoWordBrowserDescriptor descriptor = null;
          if (taskExecutor.FreeThreaded.ExecuteTask(
            "Show Files In Find Results", TaskCancelable.Yes, progress =>
            {
              progress.TaskName = "Collecting words matching '" + filterString + "'";
              progress.Start(1);

              var occurences = new List<IOccurence>();
              using (ReadLockCookie.Create())
              {
                controller.ConsumePresentableItems(
                  filterString, -1, itemsConsumer: (items, behavior) =>
                  {
                    foreach (var item in items)
                      occurences.Add(item.Occurence);
                  });
              }

              if (occurences.Count > 0 && !progress.IsCanceled)
              {
                descriptor = new GotoWordBrowserDescriptor(
                  controller.Solution, filterString, occurences);
              }

              progress.Stop();
            }))
          {
            if (descriptor != null)
              FindResultsBrowser.ShowResults(descriptor);
          }
          else
          {
            if (descriptor != null)
              descriptor.LifetimeDefinition.Terminate();
          }
        });
    }
  }
}
