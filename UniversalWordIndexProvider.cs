using JetBrains.ReSharper.Psi.ExtensionsAPI.Caches2;
using JetBrains.ReSharper.Psi.Impl.Caches2.WordIndex;

namespace JetBrains.ReSharper.ControlFlow.GoToWord
{
  public class UniversalWordIndexProvider : IWordIndexLanguageProvider
  {
    public static readonly UniversalWordIndexProvider Instance = new UniversalWordIndexProvider();
    private UniversalWordIndexProvider() { }

    public bool CaseSensitiveIdentifiers { get { return false; } }

    public bool IsIdentifierFirstLetter(char ch)
    {
      return ch.IsLetterFast() || ch == '_';
    }

    public bool IsIdentifierSecondLetter(char ch)
    {
      return ch.IsLetterFast() || ch == '_';
    }
  }
}