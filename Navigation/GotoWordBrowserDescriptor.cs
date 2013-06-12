using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.Application.Progress;
using JetBrains.IDE.TreeBrowser;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Search;
using JetBrains.ReSharper.Features.Common.Occurences;
using JetBrains.TreeModels;

namespace JetBrains.ReSharper.ControlFlow.GoToWord
{
  public class GotoWordBrowserDescriptor : OccurenceBrowserDescriptor
  {
    private readonly TreeSectionModel myModel;

    public GotoWordBrowserDescriptor(
      ISolution solution, string pattern,
      [NotNull] IEnumerable<IOccurence> occurences, IProgressIndicator indicator = null)
      : base(solution)
    {
      Title.Value = string.Format("Textual occurrences of '{0}'", pattern);
      DrawElementExtensions = true;
      myModel = new TreeSectionModel();

      using (ReadLockCookie.Create())
        SetResults(occurences.ToList(), indicator);
    }

    public override TreeModel Model
    {
      get { return myModel; }
    }

    protected override void SetResults(
      ICollection<IOccurence> items, IProgressIndicator indicator = null, bool mergeItems = true)
    {
      base.SetResults(items, indicator, mergeItems);
      RequestUpdate(UpdateKind.Structure, true);
    }
  }
}