using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.DataFlow;
using JetBrains.DocumentModel;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Hotspots;
using JetBrains.ReSharper.Feature.Services.Navigation.CustomHighlighting;
using JetBrains.TextControl;
using JetBrains.TextControl.DocumentMarkup;
using JetBrains.TextControl.Graphics;
using JetBrains.Util;
using JetBrains.Util.dataStructures.TypedIntrinsics;

namespace JetBrains.ReSharper.GoToWord
{
  internal sealed class LocalOccurrencesHighlighter
  {
    [NotNull] readonly Lifetime myLifetime;
    [NotNull] readonly IShellLocks myShellLocks;
    [NotNull] readonly ITextControl myTextControl;
    [NotNull] readonly IDocumentMarkupManager myMarkupManager;
    [NotNull] readonly SequentialLifetimes mySequentialOccurrences;
    [NotNull] readonly SequentialLifetimes mySequentialFocused;
    [NotNull] readonly object mySyncRoot;
    readonly Rect myTextControlViewportRect;

    volatile bool myShouldDropHighlightings;
    volatile bool myUpdateSelectedScheduled;
    [CanBeNull] IList<LocalOccurrence> myOccurrences;
    [CanBeNull] LocalOccurrence mySelectedOccurrence;

    public LocalOccurrencesHighlighter([NotNull] Lifetime lifetime,
                                      [NotNull] IShellLocks shellLocks,
                                      [NotNull] ITextControl textControl,
                                      [NotNull] IDocumentMarkupManager markupManager)
    {
      myLifetime = lifetime;
      myShellLocks = shellLocks;
      myMarkupManager = markupManager;
      myTextControl = textControl;
      mySequentialOccurrences = new SequentialLifetimes(lifetime);
      mySequentialFocused = new SequentialLifetimes(lifetime);
      myTextControlViewportRect = textControl.Scrolling.ViewportRect.Value;

      mySyncRoot = new object();

      myLifetime.AddAction(DropHighlightings);
    }

    public void UpdateOccurrences(
      [NotNull] IList<LocalOccurrence> occurrences,
      [NotNull] IEnumerable<LocalOccurrence> tailOccurrences = null)
    {
      lock (mySyncRoot)
      {
        myOccurrences = occurrences;
        mySelectedOccurrence = (occurrences.Count == 0) ? null : occurrences[0];

        if (!myShouldDropHighlightings && occurrences.Count == 0) return;

        mySequentialOccurrences.Next(lifetime =>
        {
          myShellLocks.ExecuteOrQueueReadLock(
            lifetime, Prefix + "UpdateOccurrence", () =>
              UpdateOccurrencesHighlighting(lifetime, occurrences));

          myUpdateSelectedScheduled = true;
        });

        mySequentialFocused.Next(lifetime =>
        {
          myShellLocks.AssertReadAccessAllowed();

          myShellLocks.ExecuteOrQueueReadLock(
            lifetime, Prefix + "UpdateOccurrence", UpdateFocusedOccurrence);
        });
      }
    }

    public void UpdateSelectedOccurrence([NotNull] LocalOccurrence occurrence)
    {
      lock (mySyncRoot)
      {
        var occurrences = myOccurrences.NotNull("occurrences != null");

        // late selection change event may happend - ignore
        if (!occurrences.Contains(occurrence)) return;

        mySelectedOccurrence = occurrence;

        if (!myUpdateSelectedScheduled)
        {
          mySequentialFocused.Next(lifetime =>
          {
            myShellLocks.ExecuteOrQueueReadLock(
              lifetime, Prefix + "UpdateSelectedOccurrence", UpdateFocusedOccurrence);
          });
        }
      }
    }

    void DropHighlightings()
    {
      lock (mySyncRoot)
      {
        mySequentialOccurrences.TerminateCurrent();
        mySequentialFocused.TerminateCurrent();

        myOccurrences = null;
        mySelectedOccurrence = null;

        if (myShouldDropHighlightings)
        {
          myShellLocks.ExecuteOrQueueReadLock(Prefix + "DropHighlightings", () =>
          {
            UpdateOccurrencesHighlighting(
              EternalLifetime.Instance, EmptyList<LocalOccurrence>.InstanceList);
            UpdateFocusedOccurrence();
          });
        }
      }
    }

    void UpdateFocusedOccurrence()
    {
      LocalOccurrence selectedOccurrence;
      lock (mySyncRoot)
      {
        selectedOccurrence = mySelectedOccurrence;
        myUpdateSelectedScheduled = false;
      }

      var documentMarkup = myMarkupManager.GetMarkupModel(myTextControl.Document);

      if (myShouldDropHighlightings)
      {
        var toRemove = new LocalList<IHighlighter>();
        foreach (var highlighter in documentMarkup.GetHighlightersEnumerable(GotoWordFocusedOccurrence))
        {
          toRemove.Add(highlighter);
        }

        foreach (var highlighter in toRemove)
        {
          documentMarkup.RemoveHighlighter(highlighter);
        }
      }

      if (selectedOccurrence != null)
      {
        var range = selectedOccurrence.Range.TextRange;
        documentMarkup.AddHighlighter(GotoWordFocusedOccurrence, range, AreaType.EXACT_RANGE, 0,
          CustomHighlightingManagerIds.NavigationHighlighterID, ErrorStripeAttributes.Empty, null);

        myShouldDropHighlightings = true;

        // todo: better positioning
        var position = Math.Max(selectedOccurrence.LineNumber - 2, 0);
        var target = myTextControl.Coords.FromDocLineColumn(
          new DocumentCoords((Int32<DocLine>)position, (Int32<DocColumn>)0));
        myTextControl.Scrolling.ScrollTo(target, TextControlScrollType.TopOfView);
      }
      else
      {
        myTextControl.Scrolling.ScrollTo(myTextControlViewportRect.Location);
      }
    }

    void UpdateOccurrencesHighlighting(
      [NotNull] Lifetime updateLifetime, [NotNull] IList<LocalOccurrence> occurrences)
    {
      if (updateLifetime.IsTerminated) return;

      var documentMarkup = myMarkupManager.GetMarkupModel(myTextControl.Document);

      // collect and remove obsolete hightlightings
      if (myShouldDropHighlightings)
      {
        var toRemove = new LocalList<IHighlighter>();

        foreach (var highlighter in documentMarkup.GetHighlightersEnumerable(GotoWordOccurrence))
        {
          if (updateLifetime.IsTerminated) return;

          toRemove.Add(highlighter);
        }

        foreach (var highlighter in toRemove)
        {
          if (updateLifetime.IsTerminated) return;


          documentMarkup.RemoveHighlighter(highlighter);
        }
      }

      // add new highlighters
      foreach (var occurrence in occurrences)
      {
        if (updateLifetime.IsTerminated) return;

        documentMarkup.AddHighlighter(
          GotoWordOccurrence, occurrence.Range.TextRange, AreaType.EXACT_RANGE, 0,
          HotspotSessionUi.CURRENT_HOTSPOT_HIGHLIGHTER, ErrorStripeUsagesAttributes, null);

        myShouldDropHighlightings = true;
      }
    }

    [NotNull] static readonly string Prefix = typeof (LocalOccurrencesHighlighter).Name + ".";

    [NotNull] static readonly Key GotoWordOccurrence = new Key("GotoWordOccurrence");
    [NotNull] static readonly Key GotoWordFocusedOccurrence = new Key("GotoWordFocusedOccurrence");

    static readonly ErrorStripeAttributes ErrorStripeUsagesAttributes =
      new ErrorStripeAttributes(ErrorStripeKind.ERROR, "ReSharper Write Usage Marker on Error Stripe");

    
  }
}