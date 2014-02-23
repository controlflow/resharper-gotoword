using System.Collections.Generic;
using JetBrains.ActionManagement;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.Application.DataContext;
using JetBrains.Application.Progress;
using JetBrains.DataFlow;
using JetBrains.ProjectModel;
using JetBrains.UI.Application.Progress;
using JetBrains.UI.Controls.GotoByName;

#if RESHARPER8
using JetBrains.ReSharper.Feature.Services.Search;
using JetBrains.ReSharper.Features.Common.FindResultsBrowser;
#elif RESHARPER81
using JetBrains.ReSharper.Feature.Services.Navigation.Search;
using JetBrains.ReSharper.Feature.Services.Occurences.Presentation;
#elif RESHARPER9
using JetBrains.ReSharper.Feature.Services.Navigation.Goto.ProvidersAPI;
using JetBrains.ReSharper.Feature.Services.Navigation;
using JetBrains.ReSharper.Feature.Services.Presentation;
using JetBrains.Application.Threading.Tasks;
using JetBrains.UI.ActionsRevised;
#endif

namespace JetBrains.ReSharper.GoToWord
{
#if RESHARPER8 || RESHARPER81

  [ActionHandler(Id)]
  public class GotoWordIndexAction : IActionHandler
  {
    public const string Id = "GotoWordIndex";

#elif RESHARPER9
  [Action(Id)]
  public class GotoWordIndexAction : IExecutableAction
  {
    public const string Id = "GotoWordIndex";
#endif

    public bool Update(IDataContext context, ActionPresentation presentation, DelegateUpdate nextUpdate)
    {
      var solution = context.GetData(ProjectModel.DataContext.DataConstants.SOLUTION);
      var isUpdate = (solution != null);

      presentation.Visible = isUpdate;

      return isUpdate;
    }

    public void Execute(IDataContext context, DelegateExecute nextExecute)
    {
      var solution = context.GetData(ProjectModel.DataContext.DataConstants.SOLUTION);
      if (solution == null) return;

      var projectFile = context.GetData(ProjectModel.DataContext.DataConstants.PROJECT_MODEL_ELEMENT) as IProjectFile;
      var textControl = context.GetData(TextControl.DataContext.DataConstants.TEXT_CONTROL);
      var initialText = context.GetData(GotoByNameDataConstants.CurrentSearchText);

      var factory = Shell.Instance.GetComponent<GotoWordControllerFactory>();
      var projectElement = (IProjectModelElement) projectFile ?? solution;

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
