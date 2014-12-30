using System.Collections.Generic;
using JetBrains.Application.ComponentModel;
using JetBrains.ReSharper.Feature.Services.Tree;
using JetBrains.ReSharper.Feature.Services.Tree.SectionsManagement;
using JetBrains.TreeModels;
using JetBrains.Util;

namespace JetBrains.ReSharper.GoToWord.Navigation.Presentation
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
        var occurrences = (section.Items.Count > 1) ? "textual occurrences" : "textual occurrence";
        var title = string.Format("Found {0} {1}", section.Items.Count, occurrences);
        sections.Add(new TreeSection(section.Model, title));
      }

      return sections;
    }
  }
}