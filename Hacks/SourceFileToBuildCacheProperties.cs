using System.Collections.Generic;
using JetBrains.Annotations;
using JetBrains.ReSharper.Psi;
using JetBrains.Util;

namespace JetBrains.ReSharper.ControlFlow.GoToWord.Hacks
{
  // NOTE: please, do not try this it at home
  internal sealed class SourceFileToBuildCacheProperties : IPsiSourceFileProperties
  {
    [NotNull] public static readonly IPsiSourceFileProperties Instance = new SourceFileToBuildCacheProperties();

    public string GetDefaultNamespace()
    {
      return string.Empty;
    }

    public IEnumerable<string> GetPreImportedNamespaces()
    {
      return EmptyList<string>.InstanceList;
    }

    public ICollection<PreProcessingDirective> GetDefines()
    {
      return EmptyList<PreProcessingDirective>.InstanceList;
    }

    public bool ShouldBuildPsi { get { return true; } }
    public bool IsGeneratedFile { get { return false; } }
    public bool IsICacheParticipant { get { return true; } }
    public bool ProvidesCodeModel { get { return true; } }
    public bool IsNonUserFile { get { return false; } }
  }
}