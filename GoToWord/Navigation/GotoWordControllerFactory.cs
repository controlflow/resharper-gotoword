using System;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.DataFlow;
using JetBrains.ProjectModel;
using JetBrains.TextControl;
using JetBrains.UI.Application;
using JetBrains.UI.Controls.GotoByName;
using JetBrains.UI.GotoByName;
using JetBrains.Util;

namespace JetBrains.ReSharper.GoToWord
{
  [ShellComponent]
  public sealed class GotoWordControllerFactory
  {
    [NotNull] private readonly IShellLocks myShellLocks;
    [NotNull] private readonly UIApplication myUiApplication;
    [NotNull] private readonly GotoByNameMenuComponent myMenuComponent;

    public GotoWordControllerFactory([NotNull] IShellLocks shellLocks,
                                     [NotNull] UIApplication uiApplication,
                                     [NotNull] GotoByNameMenuComponent menuComponent)
    {
      myShellLocks = shellLocks;
      myUiApplication = uiApplication;
      myMenuComponent = menuComponent;
    }

    public void ShowMenu([NotNull] IProjectModelElement projectElement,
                         [CanBeNull] GotoByNameDataConstants.SearchTextData initialText,
                         [CanBeNull] ITextControl textControl)
    {
      var solution = projectElement.GetSolution();
      var definition = Lifetimes.Define(solution.GetLifetime());

      var controller = new GotoWordController(
        definition.Lifetime, myShellLocks, projectElement, textControl);

      if (textControl != null)
      {
        // use selected text if there is no initial
        // todo: how to make this work with recent list?
        var selection = textControl.Selection.Ranges.Value;
        if (selection != null && selection.Count == 1)
        {
          var docRange = selection[0].ToDocRangeNormalized();
          if (docRange.Length > 0)
          {
            var selectedText = textControl.Document.GetText(docRange);
            initialText = new GotoByNameDataConstants.SearchTextData(
              selectedText, TextRange.FromLength(selectedText.Length));
          }
        }
      }

      var menu = new GotoByNameMenu(
        myMenuComponent, definition, controller.Model,
        myUiApplication.MainWindow, initialText);

      var menuDoc = menu.MenuView.Value.Document.NotNull("menuDoc != null");
      menuDoc.SelectedItem.FlowInto(definition.Lifetime, controller.SelectedItem, x => x.Key);
    }
  }
}