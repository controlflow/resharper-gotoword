using System;
using System.Linq;
using System.Xml;
using JetBrains.Application;
using JetBrains.DataFlow;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.ControlFlow.GoToWord;
using JetBrains.ReSharper.Feature.Services.Search;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.TestFramework;
using JetBrains.Threading;
using JetBrains.Util;
using NUnit.Framework;

namespace ReSharper.GoToWord.Tests.R7
{
  public abstract class GotoWordTestBase : BaseTestWithSingleProject
  {
    protected override string RelativeTestDataPath
    {
      get { return @"actions\gotoFileMember\"; }
    }

    protected void DoTest(
      string resultFileName, LibrariesFlag advancedSearch,
      string filter, string fileToTest)
    {
      var projectFiles = TestDataPath2.GetChildFiles().Select(fileInfo => fileInfo.FullPath);

      WithSingleProject(projectFiles, (lifetime, project) =>
        {
          SetAsyncBehaviorAllowed(lifetime);
          IPsiSourceFile sourceFile = null;

          RunGuarded(() =>
            {
              using (ReadLockCookie.Create())
              {
                var projectFile = project.GetSubItem(fileToTest) as IProjectFile;
                if (projectFile == null)
                  Assert.Fail("projectItem cannot be null");

                sourceFile = projectFile.ToSourceFile();
              }
            });

          var controller = new GotoWordIndexController(
            lifetime, project.GetSolution(), advancedSearch, Locks);

          var model = controller.Model;
          model.FilterText.Value = filter;
          Assert.IsFalse(model.IsReady.Value);
          JetDispatcher.Run(model.IsReady.Invert(lifetime), TimeSpan.FromSeconds(5), true); // Wait for the model to be ready
          JetDispatcher.PumpMessagesOnce(); // Any remaining operations

          // Check the item
          ExecuteWithGold(resultFileName, sw =>
            {
              using (var writer = XmlWriter.Create(sw, XmlWriterEx.WriterSettings))
              {
                writer.WriteStartElement("GotoClassTestBase");
                writer.WriteStartElement("GotoClassTestBase.GotoByNameMenuItems");

                writer.WriteAttributeString("filter", filter);
                writer.WriteAttributeString("flags", advancedSearch.ToString());

                foreach (var item in model.Items)
                  item.DumpToXaml(writer);
              }
            });
        });
    }

  }
}