using System;
using System.Collections.Generic;
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
    [NotNull] readonly Rect myInitialRect;
    [NotNull] readonly object mySyncRoot;

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
      myInitialRect = textControl.Scrolling.ViewportRect.Value;
      mySyncRoot = new object();

      myLifetime.AddAction(DropHighlightings);
    }

    public void UpdateOccurances([NotNull] IList<LocalOccurrence> occurrences)
    {
      lock (mySyncRoot)
      {
        myOccurrences = occurrences;
        mySelectedOccurrence = (occurrences.Count == 0) ? null : occurrences[0];

        if (!myShouldDropHighlightings && occurrences.Count == 0) return;

        mySequentialFocused.Next(lifetime =>
        {
          myShellLocks.ExecuteOrQueueReadLock(
            lifetime, Prefix + "UpdateOccurance", UpdateFocusedOccurence);
        });

        mySequentialOccurances.Next(lifetime =>
        {
          var interval = TimeSpan.FromMilliseconds((myOccurrences.Count > 10) ? 100 : 5);
          var updateIterator = UpdateOccurencesHighlighting(lifetime).GetEnumerator();
          lifetime.AddDispose(updateIterator);

          Action recursiveAction = null;
          recursiveAction = () =>
          {
            if (!lifetime.IsTerminated && updateIterator.MoveNext())
            {
              myShellLocks.QueueWithReadLockWhenReadLockAvailable(
                lifetime, Prefix + "UpdateOccurances", TimeSpan.FromMilliseconds(1),
                recursiveAction.NotNull());
            }
            else
            {
              updateIterator.Dispose();
            }
          };

          myShellLocks.QueueWithReadLockWhenReadLockAvailable(
            lifetime, Prefix + "UpdateOccurances", interval, recursiveAction);
          myUpdateSelectedScheduled = true;
        });
      }
    }

    public void UpdateSelectedOccurence([NotNull] LocalOccurrence occurrence)
    {
      lock (mySyncRoot)
      {
        Assertion.Assert(myOccurrences != null, "myOccurrences != null");
        Assertion.Assert(myOccurrences.Contains(occurrence), "myOccurrences.Contains(occurrence)");

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
            UpdateFocusedOccurence();
            foreach (var _ in UpdateOccurencesHighlighting(EternalLifetime.Instance)) { }
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
        var target = myTextControl.Coords.FromDocLineColumn(
          new DocumentCoords((Int32<DocLine>)(Math.Max(selectedOccurrence.LineNumber - 2, 0)), (Int32<DocColumn>)0));
        myTextControl.Scrolling.ScrollTo(target, TextControlScrollType.TopOfView);
      }
      else
      {
        myTextControl.Scrolling.ScrollTo(myInitialRect.Location);
      }
    }

    IEnumerable<bool> UpdateOccurencesHighlighting([NotNull] Lifetime updateLifetime)
    {
      if (updateLifetime.IsTerminated) yield break;

      IList<LocalOccurrence> occurences;
      lock (mySyncRoot)
      {
        occurences = myOccurrences ?? EmptyList<LocalOccurrence>.InstanceList;
      }

      var documentMarkup = myMarkupManager.GetMarkupModel(myTextControl.Document);
      var occurencesByRange = new Dictionary<TextRange, LocalOccurrence>();

      foreach (var occurence in occurences)
        occurencesByRange[occurence.Range.TextRange] = occurence;

      var operationIndex = 0;
      const int operationsPerQueue = 1000;

      // collect and remove obsolete hightlightings
      if (myShouldDropHighlightings)
      {
        var toRemove = new LocalList<IHighlighter>();

        foreach (var highlighter in documentMarkup.GetHighlightersEnumerable(GotoWordOccurance))
        {
          if (updateLifetime.IsTerminated) yield break;

          if (!occurencesByRange.Remove(highlighter.Range))
            toRemove.Add(highlighter);
        }

        foreach (var highlighter in toRemove)
        {
          if (updateLifetime.IsTerminated) yield break;
          if (operationIndex++ % operationsPerQueue == 0) yield return true;

          documentMarkup.RemoveHighlighter(highlighter);
        }
      }

      // add new highlighters
      foreach (var occurrence in occurencesByRange)
      {
        if (updateLifetime.IsTerminated) yield break;
        if (operationIndex++ % operationsPerQueue == 0) yield return true;

        documentMarkup.AddHighlighter(GotoWordOccurance, occurrence.Key, AreaType.EXACT_RANGE, 0,
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