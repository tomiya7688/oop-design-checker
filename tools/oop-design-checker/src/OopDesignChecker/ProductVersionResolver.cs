using System.Reflection;

namespace OopDesignChecker;

/*
{
  責務: CUIとGUIで共通のproduct version表示値を解決する
  処理: [informational versionを優先し、assembly version、unknownの順にfallbackする]
}
*/
public static class ProductVersionResolver
{
    /*
    {
      責務: 指定されたassemblyのproduct version表示値を返す
      引数: [assembly: product versionを読み取るassembly]
      戻り値: '+'以降を除いたinformational version、またはfallback version
    }
    */
    public static string Resolve(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return Resolve(
            assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion,
            assembly.GetName().Version
        );
    }

    /*
    {
      責務: assembly metadataの値から表示versionを決める
      処理: [informational version、3要素assembly version、unknownの順に選択する]
      引数: [informationalVersion: assemblyの製品version属性値, assemblyVersion: assembly version]
      戻り値: CLIに表示するversion文字列
    }
    */
    internal static string Resolve(string? informationalVersion, Version? assemblyVersion)
    {
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion.Split('+', 2)[0];
        }

        return assemblyVersion?.ToString(3) ?? "unknown";
    }
}
