using System.Collections.Generic;
using JetBrains.Annotations;
using JetBrains.Application.Progress;
using JetBrains.IDE.TreeBrowser;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Occurences;
using JetBrains.ReSharper.Feature.Services.Tree;
using JetBrains.ReSharper.Resources.Shell;
using JetBrains.TreeModels;

namespace JetBrains.ReSharper.GoToWord.Navigation.Presentation
{
  public sealed class GotoWordBrowserDescriptor : OccurenceBrowserDescriptor
  {
    [NotNull] private readonly TreeSectionModel myModel;

    public GotoWordBrowserDescriptor(
      [NotNull] ISolution solution, [NotNull] string pattern, [NotNull] List<IOccurence> occurrences, [CanBeNull] IProgressIndicator indicator = null)
      : base(solution)
    {
      Title.Value = string.Format("Textual occurrences of '{0}'", pattern);
      DrawElementExtensions = true;
      myModel = new TreeSectionModel();

      using (ReadLockCookie.Create())
      {
        SetResults(occurrences, indicator);
      }
    }

    public override TreeModel Model
    {
      get { return myModel; }
    }

    protected override void SetResults(ICollection<IOccurence> items, IProgressIndicator indicator = null, bool mergeItems = true)
    {
      base.SetResults(items, indicator, mergeItems);
      RequestUpdate(UpdateKind.Structure, true);
    }
  }
}