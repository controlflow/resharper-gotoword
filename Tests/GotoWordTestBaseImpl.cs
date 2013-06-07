using System.IO;
using JetBrains.ReSharper.Feature.Services.Search;

namespace ReSharper.GoToWord.Tests.R7
{
  class GotoWordTestBaseImpl : GotoWordTestBase
  {
    //[TestCase("test1", LibrariesFlag.SolutionOnly, "", "ChildAndPart.cs")]
    //[TestCase("test2", LibrariesFlag.SolutionAndLibraries, "", "ChildAndPart.cs")]
    //[TestCase("test3", LibrariesFlag.SolutionAndLibraries, "public", "ChildAndPart.cs")]
    //[TestCase("test4", LibrariesFlag.SolutionOnly, "cl", "ChildAndPart.cs")]
    //[TestCase("test5", LibrariesFlag.SolutionAndLibraries, "*c", "ChildAndPart.cs")]
    //[TestCase("test6", LibrariesFlag.SolutionOnly, "this", "ChildAndPart.cs")]
    //[TestCase("test7", LibrariesFlag.SolutionAndLibraries, "ctor", "ChildAndPart.cs")]
    public void Test(string resultFileName, LibrariesFlag advancedSearch, string filter, string fileToTest)
    {
      DoTest(resultFileName, advancedSearch, filter, fileToTest);
    }
  }
}