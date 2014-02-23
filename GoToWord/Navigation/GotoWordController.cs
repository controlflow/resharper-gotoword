using System;
using System.Collections.Generic;
using System.Drawing;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.DataFlow;
using JetBrains.DocumentModel;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Daemon;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Hotspots;
using JetBrains.ReSharper.Feature.Services.Navigation.CustomHighlighting;
using JetBrains.ReSharper.Feature.Services.Navigation.Occurences;
using JetBrains.ReSharper.Feature.Services.Navigation.Search;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Resources;
using JetBrains.ReSharper.Psi.Services.Presentation;
using JetBrains.TextControl;
using JetBrains.TextControl.DocumentMarkup;
using JetBrains.TextControl.Graphics;
using JetBrains.Threading;
using JetBrains.UI.GotoByName;
using JetBrains.UI.Icons;
using JetBrains.UI.PopupMenu;
using JetBrains.UI.PopupMenu.Impl;
using JetBrains.UI.RichText;
using JetBrains.Util;
using JetBrains.Util.dataStructures.TypedIntrinsics;

namespace JetBrains.ReSharper.GoToWord
{
  // todo: show recent items when filter is empty :)
  // todo: highlight occurances in current file
  // todo: use menu separator!
  // todo: newlines in filter text
  // todo: support escaping in search text?

  public sealed class GotoWordController : GotoByNameController
  {
    [NotNull] private readonly IShellLocks myLocks;
    [NotNull] private readonly IProjectModelElement myProjectElement;
    [CanBeNull] private readonly IPsiSourceFile myCurrentFile;
    [CanBeNull] private readonly ITextControl myTextControl;

    private volatile bool myShouldDropHighlightings;
    private List<LocalOccurrence> myLocal;

    public GotoWordController(
      [NotNull] Lifetime lifetime, [NotNull] IShellLocks locks,
      [NotNull] IProjectModelElement projectElement, [CanBeNull] ITextControl textControl)
      : base(lifetime, new GotoByNameModel(lifetime), locks)
    {
      myLocks = locks;
      myTextControl = textControl;
      myProjectElement = projectElement;

      var projectFile = projectElement as IProjectFile;
      if (projectFile != null)
      {
        myCurrentFile = projectFile.ToSourceFile();
      }

      lifetime.AddAction(DropHighlightings);

      SelectedItem = new Property<object>(lifetime, "SelectedItem");
      SelectedItem.Change.Advise(lifetime, x =>
      {
        if (x.HasNew)
        {
          var localOccurrence = x.New as LocalOccurrence;
          if (localOccurrence != null)
          {
            if (textControl != null)
            {
              //var textControlPosRange = textControl.Scrolling.ViewportRange.Value;
              //GC.KeepAlive(textControlPosRange);

              myLocks.QueueReadLock("Aa", () =>
              {
                // todo: merge with highlighting updater?
                var target = textControl.Coords.FromDocLineColumn(
                  new DocumentCoords((Int32<DocLine>)(Math.Max(localOccurrence.LineNumber - 2, 0)), (Int32<DocColumn>)0));
                textControl.Scrolling.ScrollTo(target, TextControlScrollType.TopOfView);
              });

              if (myLocal != null)
              {
                UpdateLocalHighlightings(textControl.Document, myLocal);
              }
            }
          }
        }
      });

      InitializeModel(lifetime, Model);
    }

    private static void InitializeModel([NotNull] Lifetime lifetime, [NotNull] GotoByNameModel model)
    {
      model.IsCheckBoxCheckerVisible.FlowInto(
        lifetime, model.CheckBoxText, flag => flag ? "Middle match" : string.Empty);

      model.CaptionText.Value = "Enter words:";
      model.NotReadyMessage.Value = "Some textual occurrences may be missing at the moment";
    }

    public IProperty<object> SelectedItem { get; private set; }

    protected override bool ExecuteItem(JetPopupMenuItem item, ISignal<bool> closeBeforeExecute)
    {
      

      return true;
    }

    protected override bool UpdateItems(
      string filterString, Func<IEnumerable<JetPopupMenuItem>, AddItemsBehavior, bool> itemsConsumer)
    {
      var currentFile = myCurrentFile;
      if (currentFile != null)
      {
        if (filterString.Length > 0)
        {
          var occurences = new List<LocalOccurrence>();
          SearchInCurrentFile(filterString, currentFile, occurences);
          var xs = new List<JetPopupMenuItem>();

          var presentationService = Shell.Instance.GetComponent<PsiSourceFilePresentationService>();
          var sourceFileIcon = presentationService.GetIconId(currentFile);
          var displayName = currentFile.Name;

          foreach (var occurence in occurences)
          {
            var descriptor = new Foo1(occurence, displayName, sourceFileIcon);
            var item = new JetPopupMenuItem(occurence, descriptor);
            xs.Add(item);
          }

          itemsConsumer(xs, AddItemsBehavior.Replace);

          if (myTextControl != null)
          {
            UpdateLocalHighlightings(myTextControl.Document, occurences);
          }

          myLocal = occurences;
        }
        else
        {
          DropHighlightings();
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

    [NotNull] private static readonly Key GotoWordHighlightings = new Key("GotoWordHighlightings");

    private void UpdateLocalHighlightings(
      [NotNull] IDocument document, [NotNull] IList<LocalOccurrence> occurences)
    {
      myLocks.QueueReadLock("GoToWord.UpdateLocalHighlightings", () =>
      {
        var markupManager = Shell.Instance.GetComponent<IDocumentMarkupManager>();
        var markup = markupManager.GetMarkupModel(document);

        var selected = SelectedItem.Value as LocalOccurrence;

        var hs = new LocalList<IHighlighter>();
        hs.AddRange(markup.GetHighlightersEnumerable(GotoWordHighlightings));
        foreach (var highlighter in hs) markup.RemoveHighlighter(highlighter);

        if (occurences.Count == 0) { myShouldDropHighlightings = false; return; }

        var errorStripeAttributes = new ErrorStripeAttributes(
          ErrorStripeKind.ERROR, "ReSharper Write Usage Marker on Error Stripe");

        foreach (var occurrence in occurences)
        {
          markup.AddHighlighter(
            GotoWordHighlightings, occurrence.Range.TextRange, AreaType.EXACT_RANGE, 0,
            HotspotSessionUi.CURRENT_HOTSPOT_MIRRORS_HIGHLIGHTER, errorStripeAttributes, null);

          if (occurrence == selected)
          {
            markup.AddHighlighter(
              GotoWordHighlightings, occurrence.Range.TextRange, AreaType.EXACT_RANGE, 0,
              CustomHighlightingManagerIds.NavigationHighlighterID, ErrorStripeAttributes.Empty, null);
          }

          myShouldDropHighlightings = true;
        }
      });
    }

    private void DropHighlightings()
    {
      // todo: bring back original viewport

      var textControl = myTextControl;
      if (textControl != null && myShouldDropHighlightings)
      {
        UpdateLocalHighlightings(textControl.Document, EmptyList<LocalOccurrence>.InstanceList);
      }
    }

    // todo: make lazy
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