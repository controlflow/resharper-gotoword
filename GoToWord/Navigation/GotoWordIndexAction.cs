using System.Collections.Generic;
using JetBrains.ActionManagement;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.Application.DataContext;
using JetBrains.Application.Progress;
using JetBrains.DataFlow;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Occurences;
using JetBrains.ReSharper.Feature.Services.Presentation;
using JetBrains.ReSharper.GoToWord.Navigation.Presentation;
using JetBrains.ReSharper.Resources.Shell;
using JetBrains.UI.ActionsRevised;
using JetBrains.UI.Application.Progress;
using JetBrains.UI.Controls.GotoByName;
using DataConstants = JetBrains.ProjectModel.DataContext.DataConstants;

namespace JetBrains.ReSharper.GoToWord
{
  [Action(Id)]
  public class GotoWordIndexAction : IExecutableAction
  {
    public const string Id = "GotoWordIndex";

    public bool Update(IDataContext context, ActionPresentation presentation, DelegateUpdate nextUpdate)
    {
      var solution = context.GetData(DataConstants.SOLUTION);
      var isUpdate = (solution != null);

      presentation.Visible = isUpdate;

      return isUpdate;
    }

    public void Execute(IDataContext context, DelegateExecute nextExecute)
    {
      var solution = context.GetData(DataConstants.SOLUTION);
      if (solution == null) return;

      var projectFile = context.GetData(DataConstants.PROJECT_MODEL_ELEMENT) as IProjectFile;
      var textControl = context.GetData(TextControl.DataContext.DataConstants.TEXT_CONTROL);
      var initialText = context.GetData(GotoByNameDataConstants.CurrentSearchText);

      var projectElement = (IProjectModelElement) projectFile ?? solution;
      var factory = Shell.Instance.GetComponent<GotoWordControllerFactory>();

      factory.ShowMenu(projectElement, textControl, initialText);
    }

    // todo: rewrite
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

              var occurrences = new List<IOccurence>();
              using (ReadLockCookie.Create())
              {
                controller.ConsumePresentableItems(
                  filterString, -1, itemsConsumer: (items, behavior) =>
                  {
                    foreach (var item in items)
                      occurrences.Add(item.Occurence);
                  });
              }

              if (occurrences.Count > 0 && !progress.IsCanceled)
              {
                descriptor = new GotoWordBrowserDescriptor(
                  controller.Solution, filterString, occurrences);
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
