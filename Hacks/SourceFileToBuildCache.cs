using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using JetBrains.DocumentModel;
using JetBrains.Metadata.Reader.API;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Modules;
using JetBrains.Util;

namespace JetBrains.ReSharper.ControlFlow.GoToWord.Hacks
{
  // NOTE: please, do not try this it at home
  internal sealed class SourceFileToBuildCache : IPsiSourceFile
  {
    [NotNull] private readonly IPsiSourceFile myOriginal;

    public SourceFileToBuildCache([NotNull] IPsiSourceFile psiSourceFile)
    {
      myOriginal = psiSourceFile;
    }

    public void PutData<T>(Key<T> key, T val) where T : class
    {
      myOriginal.PutData(key, val);
    }

    public T GetData<T>(Key<T> key) where T : class
    {
      return myOriginal.GetData(key);
    }

    public IEnumerable<KeyValuePair<object, object>> EnumerateData()
    {
      return myOriginal.EnumerateData();
    }

    public bool IsValid()
    {
      return myOriginal.IsValid();
    }

    public string GetPersistentID()
    {
      return myOriginal.GetPersistentID();
    }

    public IPsiModule PsiModule
    {
      get { return myOriginal.PsiModule; }
    }

    public IDocument Document
    {
      get { return myOriginal.Document; }
    }

    public string Name
    {
      get { return myOriginal.Name; }
    }

    public string DisplayName
    {
      get { return myOriginal.DisplayName; }
    }

    public ProjectFileType LanguageType
    {
      get { return myOriginal.LanguageType; }
    }

    public PsiLanguageType PrimaryPsiLanguage
    {
      get { return myOriginal.PrimaryPsiLanguage; }
    }

    public IPsiSourceFileProperties Properties
    {
      get { return SourceFileToBuildCacheProperties.Instance; }
    }

    public IPsiSourceFileStorage PsiStorage
    {
      get { return myOriginal.PsiStorage; }
    }

    public int? InMemoryModificationStamp
    {
      get { return myOriginal.InMemoryModificationStamp; }
    }

    public DateTime LastWriteTimeUtc
    {
      get { return myOriginal.LastWriteTimeUtc; }
    }

    public IModuleReferenceResolveContext ResolveContext
    {
      get { return myOriginal.ResolveContext; }
    }

    // NOTE: IMPORTANT
    public override int GetHashCode()
    {
      return myOriginal.GetHashCode();
    }

    public override bool Equals(object obj)
    {
      return myOriginal.Equals(obj);
    }
  }
}