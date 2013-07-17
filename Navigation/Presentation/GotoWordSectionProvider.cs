using System.Collections.Generic;
using JetBrains.ReSharper.Features.Common.Occurences;
using JetBrains.ReSharper.Psi;
using JetBrains.TreeModels;
using JetBrains.Util;

namespace JetBrains.ReSharper.ControlFlow.GoToWord
{
  [ShellFeaturePart]
  public class GotoWordSectionProvider : OccurenceSectionProvider
  {
    public override bool IsApplicable(OccurenceBrowserDescriptor descriptor)
    {
      return descriptor is GotoWordBrowserDescriptor;
    }

    public override ICollection<TreeSection> GetTreeSections(OccurenceBrowserDescriptor descriptor)
    {
      var browserDescriptor = descriptor as GotoWordBrowserDescriptor;
      if (browserDescriptor == null)
        return EmptyList<TreeSection>.InstanceList;

      var sections = new List<TreeSection>();
      foreach (var section in descriptor.OccurenceSections)
      {
        var occurances = (section.Items.Count > 1) ? "textual occurances" : "textual occurance";
        var title = string.Format("Found {0} {1}", section.Items.Count, occurances);
        sections.Add(new TreeSection(section.Model, title));
      }

      return sections;
    }
  }
}