using System;
using System.Collections.Generic;
using System.Drawing;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.DataFlow;
using JetBrains.DocumentModel;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Presentation;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Resources;
using JetBrains.ReSharper.Resources.Shell;
using JetBrains.TextControl;
using JetBrains.TextControl.DocumentMarkup;
using JetBrains.UI.GotoByName;
using JetBrains.UI.Icons;
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

  public sealed class GotoWordController : GotoByNameController
  {
    [NotNull] readonly IShellLocks myShellLocks;
    [NotNull] readonly IProjectModelElement myProjectElement;
    [NotNull] readonly IProperty<object> mySelectedItem;
    [CanBeNull] readonly IPsiSourceFile myCurrentFile;
    [CanBeNull] readonly ITextControl myTextControl;
    [CanBeNull] readonly LocalOccurrencesHighlighter myHighlighter;

    public GotoWordController([NotNull] Lifetime lifetime,
                              [NotNull] IShellLocks shellLocks,
                              [NotNull] IProjectModelElement projectElement,
                              [CanBeNull] ITextControl textControl,
                              [NotNull] IDocumentMarkupManager markupManager)
      : base(lifetime, new GotoByNameModel(lifetime), shellLocks)
    {
      myShellLocks = shellLocks;
      myTextControl = textControl;
      myProjectElement = projectElement;
      mySelectedItem = new Property<object>(lifetime, "SelectedItem");

      var projectFile = projectElement as IProjectFile;
      if (projectFile != null)
      {
        myCurrentFile = projectFile.ToSourceFile();

        if (textControl != null)
        {
          myHighlighter = new LocalOccurrencesHighlighter(
            lifetime, shellLocks, textControl, markupManager);
          SelectedItem.Change.Advise(lifetime, AdviceSelectionItem);
        }
      }

      InitializeModel(lifetime, Model);
    }

    // Currently selected popup menu item
    [NotNull] public IProperty<object> SelectedItem
    {
      get { return mySelectedItem; }
    }

    void AdviceSelectionItem([NotNull] PropertyChangedEventArgs<object> args)
    {
      if (!args.HasNew) return;

      var occurrence = args.New as LocalOccurrence;
      if (occurrence != null)
      {
        myHighlighter.NotNull().UpdateSelectedOccurrence(occurrence);
      }
    }

    static void InitializeModel([NotNull] Lifetime lifetime, [NotNull] GotoByNameModel model)
    {
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
      // todo: drop highlightings when empty filter
      // todo: fill recent searches when empty
      // todo: what thread is it?

      var currentFile = myCurrentFile;
      if (currentFile != null)
      {
        if (filterString.Length > 0)
        {
          var lazyOccurences = SearchInCurrentFile(filterString, currentFile);
          var presentationService = Shell.Instance.GetComponent<PsiSourceFilePresentationService>();
          var sourceFileIcon = presentationService.GetIconId(currentFile);
          var displayName = currentFile.Name;

          IEnumerable<LocalOccurrence> tailConcurrences;
          var occurences = lazyOccurences.TakeFirstAndTail(40, out tailConcurrences);

          var menuItems = new List<JetPopupMenuItem>();
          foreach (var localOccurrence in occurences)
          {
            var descriptor = new Foo1(localOccurrence, displayName, sourceFileIcon);
            var item = new JetPopupMenuItem(localOccurrence, descriptor);
            menuItems.Add(item);
          }

          itemsConsumer(menuItems, AddItemsBehavior.Replace);

          if (myHighlighter != null)
            myHighlighter.UpdateOccurrences(occurences, tailConcurrences);
        }
        else
        {
          // todo: do not do it? revert viewport?
          if (myHighlighter != null)
            myHighlighter.UpdateOccurrences(EmptyList<LocalOccurrence>.InstanceList);
        }
      }

      return false;
    }

    

    private static IEnumerable<LocalOccurrence> SearchInCurrentFile(
      [NotNull] string searchText, [NotNull] IPsiSourceFile sourceFile)
    {
      var document = sourceFile.Document;

      var fileText = document.GetText();
      if (fileText == null) yield break;

      var offset = 0;
      while ((offset = fileText.IndexOf(searchText, offset, StringComparison.OrdinalIgnoreCase)) >= 0)
      {
        var occurrenceRange = TextRange.FromLength(offset, searchText.Length);
        var documentRange = new DocumentRange(document, occurrenceRange);
        var documentLine = (int) document.GetCoordsByOffset(offset).Line;

        var foundText = fileText.Substring(offset, searchText.Length);

        var leftIndex = Math.Max(0, offset - 10);
        var leftFragment = fileText.Substring(leftIndex, offset - leftIndex);

        var endOffset = offset + searchText.Length;
        var rightIndex = Math.Min(endOffset + 10, fileText.Length);
        var rightFragment = fileText.Substring(endOffset, rightIndex - endOffset);

        yield return new LocalOccurrence(
          documentRange, documentLine, foundText, leftFragment, rightFragment);

        offset++;
      }
    }

    private class Foo1 : MenuItemDescriptor
    {
      public Foo1(LocalOccurrence occurrence, string displayName, IconId sourceFileIcon)
        : base(displayName + ":" + occurrence.LineNumber)
      {
        Icon = sourceFileIcon ?? PsiSymbolsThemedIcons.Const.Id;
        Style = MenuItemStyle.Enabled;

        //var left = Gradient(100, 255, occurrence.LeftFragment, SystemColors.GrayText);
        //var right = Gradient(255, 100, occurrence.RightFragment, SystemColors.GrayText);

        ShortcutText = RichText.Empty
          .Append(occurrence.LeftFragment, TextStyle.FromForeColor(SystemColors.GrayText))
          .Append(occurrence.FoundText, new TextStyle(FontStyle.Bold, TextStyle.DefaultForegroundColor))
          .Append(occurrence.RightFragment, TextStyle.FromForeColor(SystemColors.GrayText));
      }
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

  static class EnumerableEx
  {
    [NotNull] public static IList<T> TakeFirstAndTail<T>(
      [NotNull] this IEnumerable<T> source, int count, [NotNull] out IEnumerable<T> tailItems)
    {
      Assertion.Assert(count >= 0, "count >= 0");

      var hasTail = false;
      var list = new LocalList<T>();
      var enumerator = source.GetEnumerator();
      try
      {
        while (count > 0)
        {
          if (enumerator.MoveNext())
          {
            list.Add(enumerator.Current);
            count--;
          }
          else
          {
            tailItems = EmptyList<T>.InstanceList;
            return list.ResultingList();
          }
        }

        hasTail = true;
        tailItems = TailEnumerable(enumerator);
        return list.ResultingList();
      }
      finally
      {
        if (!hasTail) enumerator.Dispose();
      }
    }

    [NotNull]
    private static IEnumerable<T> TailEnumerable<T>([NotNull] IEnumerator<T> enumerator)
    {
      using (enumerator)
      {
        while (enumerator.MoveNext())
        {
          yield return enumerator.Current;
        }
      }
    }
  }
}