using System;
using System.Collections.Generic;
using System.Drawing;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.DataFlow;
using JetBrains.DocumentModel;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Navigation.Occurences;
using JetBrains.ReSharper.Feature.Services.Navigation.Search;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Resources;
using JetBrains.ReSharper.Psi.Services.Presentation;
using JetBrains.UI.GotoByName;
using JetBrains.UI.PopupMenu;
using JetBrains.UI.PopupMenu.Impl;
using JetBrains.UI.RichText;
using JetBrains.Util;

namespace JetBrains.ReSharper.GoToWord
{
  // todo: show recent items when filter is empty :)
  // todo: highlight occurances in current file
  // todo: use menu separator!
  // todo: newlines in filter text
  // todo: support escaping in search text?

  public class GotoWordController : GotoByNameController
  {
    private readonly IProjectFile myProjectFile;

    public GotoWordController([NotNull] Lifetime lifetime, [NotNull] IShellLocks locks,
      [CanBeNull] IProjectFile projectFile)
      : base(lifetime, new GotoByNameModel(lifetime), locks)
    {
      myProjectFile = projectFile;
      // init model
      var model = Model;

      model.IsCheckBoxCheckerVisible.FlowInto(
        lifetime, model.CheckBoxText, flag => flag ? "Middle match" : string.Empty);

      model.CaptionText.Value = "Enter words:";
      model.NotReadyMessage.Value = "Some textual occurrences may be missing at the moment";
    }

    protected override bool ExecuteItem(JetPopupMenuItem item, ISignal<bool> closeBeforeExecute)
    {
      

      return true;
    }

    protected override bool UpdateItems(
      string filterString, Func<IEnumerable<JetPopupMenuItem>, AddItemsBehavior, bool> itemsConsumer)
    {
      if (myProjectFile != null)
      {
        var sourceFile = myProjectFile.ToSourceFile();
        if (sourceFile != null && filterString.Length > 0)
        {
          var occurences = new List<LocalOccurrence>();
          SearchInCurrentFile(filterString, sourceFile, occurences);
          var xs = new List<JetPopupMenuItem>();

          foreach (var occurence in occurences)
          {
            var item = new JetPopupMenuItem(occurence, new Foo1(occurence));
            xs.Add(item);
          }

          itemsConsumer(xs, AddItemsBehavior.Replace);
        }
      }


      //itemsConsumer(new JetPopupMenuItem[]
      //{
      //  new JetPopupMenuItem("aa", new SimpleMenuItem(filterString, null, () => { }))
      //
      //}, AddItemsBehavior.Replace);



      //itemsConsumer(null, AddItemsBehavior.Append);

      return false;
    }

    private static void SearchInCurrentFile(
      [NotNull] string searchText,
      [NotNull] IPsiSourceFile sourceFile,
      [NotNull] List<LocalOccurrence> consumer)
    {
      var document = sourceFile.Document;

      var fileText = document.GetText();
      if (fileText == null) return;

      var offset = 0;
      while ((offset = fileText.IndexOf(searchText, offset, StringComparison.OrdinalIgnoreCase)) >= 0)
      {
        var occurrenceRange = TextRange.FromLength(offset, searchText.Length);
        var documentRange = new DocumentRange(document, occurrenceRange);
        var coords = (int) document.GetCoordsByOffset(offset).Line;

        var foundText = fileText.Substring(offset, searchText.Length);

        var leftIndex = Math.Max(0, offset - 10);
        var leftFragment = fileText.Substring(leftIndex, offset - leftIndex);

        var endOffset = offset + searchText.Length;
        var rightIndex = Math.Min(endOffset + 10, fileText.Length);
        var rightFragment = fileText.Substring(endOffset, rightIndex - endOffset);

        consumer.Add(new LocalOccurrence(
          documentRange, coords, foundText, leftFragment, rightFragment));

        offset++;
      }
    }

    sealed class LocalOccurrence
    {
      private readonly DocumentRange myRange;
      private readonly int myLineNumber;
      private readonly string myFoundText, myLeftFragment, myRightFragment;

      public LocalOccurrence(DocumentRange range, int lineNumber,
        string foundText, string leftFragment, string rightFragment)
      {
        myRange = range;
        myLineNumber = lineNumber;
        myFoundText = foundText;
        myLeftFragment = leftFragment;
        myRightFragment = rightFragment;
      }

      public DocumentRange Range { get { return myRange; } }
      public int LineNumber { get { return myLineNumber; } }

      public string FoundText { get { return myFoundText; } }
      public string LeftFragment { get { return myLeftFragment; } }
      public string RightFragment { get { return myRightFragment; } }
    }

    private class Foo1 : MenuItemDescriptor
    {
      public Foo1(LocalOccurrence occurrence) : base("Line " + occurrence.LineNumber)
      {
        Icon = PsiSymbolsThemedIcons.Const.Id;
        Style = MenuItemStyle.Enabled;


        var left = Gradient(100, 255, occurrence.LeftFragment, SystemColors.GrayText);
        var right = Gradient(255, 100, occurrence.RightFragment, SystemColors.GrayText);

        ShortcutText = left.Append(occurrence.FoundText).Append(right);
      }

      RichText Gradient(int from, int to, string text, Color baseColor)
      {
        var richText = RichText.Empty;
        if (text.Length == 0) return richText;

        var step = Math.Abs(from - to) / (double) text.Length;
        if (from > to) step = -step;

        var alpha = from;

        foreach (var ch in text)
        {
          var foreColor = TextStyle.FromForeColor(Color.FromArgb(alpha, baseColor));
          richText = richText.Append(char.ToString(ch), foreColor);

          alpha = (int)(alpha + step);
        }

        return richText;
      }
    }
  }

  
}