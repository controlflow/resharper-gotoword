using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.DataFlow;
using JetBrains.DocumentModel;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Hotspots;
using JetBrains.ReSharper.Feature.Services.Navigation.CustomHighlighting;
using JetBrains.TextControl;
using JetBrains.TextControl.DocumentMarkup;
using JetBrains.TextControl.Graphics;
using JetBrains.Threading;
using JetBrains.Util;
using JetBrains.Util.dataStructures.TypedIntrinsics;

namespace JetBrains.ReSharper.GoToWord
{
  internal sealed class LocalOccurancesHighlighter
  {
    [NotNull] readonly Lifetime myLifetime;
    [NotNull] readonly IShellLocks myShellLocks;
    [NotNull] readonly ITextControl myTextControl;
    [NotNull] readonly IDocumentMarkupManager myMarkupManager;
    [NotNull] readonly SequentialLifetimes mySequentialOccurances;
    [NotNull] readonly SequentialLifetimes mySequentialFocused;
    [NotNull] readonly object mySyncRoot;
    readonly Rect myTextControlViewportRect;

    volatile bool myShouldDropHighlightings;
    volatile bool myUpdateSelectedScheduled;
    [CanBeNull] IList<LocalOccurrence> myOccurrences;
    [CanBeNull] LocalOccurrence mySelectedOccurrence;

    public LocalOccurancesHighlighter([NotNull] Lifetime lifetime,
                                      [NotNull] IShellLocks shellLocks,
                                      [NotNull] ITextControl textControl,
                                      [NotNull] IDocumentMarkupManager markupManager)
    {
      myLifetime = lifetime;
      myShellLocks = shellLocks;
      myMarkupManager = markupManager;
      myTextControl = textControl;
      mySequentialOccurances = new SequentialLifetimes(lifetime);
      mySequentialFocused = new SequentialLifetimes(lifetime);
      myTextControlViewportRect = textControl.Scrolling.ViewportRect.Value;

      mySyncRoot = new object();

      myLifetime.AddAction(DropHighlightings);
    }

    public void UpdateOccurances(
      [NotNull] IList<LocalOccurrence> occurrences,
      [NotNull] IEnumerable<LocalOccurrence> tailOccurences = null)
    {
      lock (mySyncRoot)
      {
        myOccurrences = occurrences;
        mySelectedOccurrence = (occurrences.Count == 0) ? null : occurrences[0];

        if (!myShouldDropHighlightings && occurrences.Count == 0) return;

        mySequentialOccurances.Next(lifetime =>
        {
          myShellLocks.ExecuteOrQueueReadLock(
            lifetime, Prefix + "UpdateOccurance", () =>
              UpdateOccurencesHighlighting(lifetime, occurrences));

          myUpdateSelectedScheduled = true;
        });

        mySequentialFocused.Next(lifetime =>
        {
          myShellLocks.AssertReadAccessAllowed();

          myShellLocks.ExecuteOrQueueReadLock(
            lifetime, Prefix + "UpdateOccurance", UpdateFocusedOccurence);
        });
      }
    }

    public void UpdateSelectedOccurence([NotNull] LocalOccurrence occurrence)
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
              lifetime, Prefix + "UpdateSelectedOccurence", UpdateFocusedOccurence);
          });
        }
      }
    }

    void DropHighlightings()
    {
      lock (mySyncRoot)
      {
        mySequentialOccurances.TerminateCurrent();
        mySequentialFocused.TerminateCurrent();

        myOccurrences = null;
        mySelectedOccurrence = null;

        if (myShouldDropHighlightings)
        {
          myShellLocks.ExecuteOrQueueReadLock(Prefix + "DropHighlightings", () =>
          {
            UpdateOccurencesHighlighting(
              EternalLifetime.Instance, EmptyList<LocalOccurrence>.InstanceList);
            UpdateFocusedOccurence();
          });
        }
      }
    }

    void UpdateFocusedOccurence()
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
        foreach (var highlighter in documentMarkup.GetHighlightersEnumerable(GotoWordFocusedOccurrance))
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
        documentMarkup.AddHighlighter(GotoWordFocusedOccurrance, range, AreaType.EXACT_RANGE, 0,
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

    void UpdateOccurencesHighlighting(
      [NotNull] Lifetime updateLifetime, [NotNull] IList<LocalOccurrence> occurences)
    {
      if (updateLifetime.IsTerminated) return;

      var documentMarkup = myMarkupManager.GetMarkupModel(myTextControl.Document);

      // collect and remove obsolete hightlightings
      if (myShouldDropHighlightings)
      {
        var toRemove = new LocalList<IHighlighter>();

        foreach (var highlighter in documentMarkup.GetHighlightersEnumerable(GotoWordOccurance))
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
      foreach (var occurrence in occurences)
      {
        if (updateLifetime.IsTerminated) return;

        documentMarkup.AddHighlighter(
          GotoWordOccurance, occurrence.Range.TextRange, AreaType.EXACT_RANGE, 0,
          HotspotSessionUi.CURRENT_HOTSPOT_HIGHLIGHTER, ErrorStripeUsagesAttributes, null);

        myShouldDropHighlightings = true;
      }
    }

    [NotNull] static readonly string Prefix = typeof (LocalOccurancesHighlighter).Name + ".";

    [NotNull] static readonly Key GotoWordOccurance = new Key("GotoWordOccurance");
    [NotNull] static readonly Key GotoWordFocusedOccurrance = new Key("GotoWordFocusedOccurrance");

    static readonly ErrorStripeAttributes ErrorStripeUsagesAttributes =
      new ErrorStripeAttributes(ErrorStripeKind.ERROR, "ReSharper Write Usage Marker on Error Stripe");

    
  }
}