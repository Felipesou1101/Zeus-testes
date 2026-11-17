namespace ZeusFiscalMonitor.Testes.Helpers;

internal static class XmlExemplos
{
    public const string ChaveNFe = "28261111222333000181550010000001231123456782";
    public const string ChaveNFCe = "28261111222333000181650010000001241123456790";

    public static string NFe => Ler("nfe_exemplo.xml");
    public static string NFCe => Ler("nfce_exemplo.xml");

    private static string Ler(string arquivo) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Exemplos", arquivo));
}
