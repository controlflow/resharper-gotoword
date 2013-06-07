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
        lifetime, model.CheckBoxText, value =>
          value ? "Case sensitive" : "Case insensitive");

      model.CaptionText.Value = "Enter words:";
      model.NotReadyMessage.Value = "Some textual occurances may be missing at the moment";
    }
  }
}