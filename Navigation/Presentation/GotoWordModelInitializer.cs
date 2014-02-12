using JetBrains.DataFlow;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Features.Common.ComponentsAPI;
using JetBrains.UI.GotoByName;

namespace JetBrains.ReSharper.ControlFlow.GoToWord
{
  [SolutionComponent]
  public class GotoWordModelInitializer : IModelInitializer
  {
    public void InitModel(Lifetime lifetime, GotoByNameModel model)
    {
      model.IsCheckBoxCheckerVisible.FlowInto(
        lifetime, model.CheckBoxText, flag => flag ? "Middle match" : string.Empty);

      model.CaptionText.Value = "Enter words:";
      model.NotReadyMessage.Value = "Some textual occurrences may be missing at the moment";
    }
  }
}