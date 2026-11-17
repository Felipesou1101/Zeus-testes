using ZeusFiscalMonitor.Domain.Exceptions;

namespace ZeusFiscalMonitor.Domain.ValueObjects;

/// <summary>
/// Unidade Federativa brasileira. Só pode existir em estado válido (uma das 27 UFs).
/// Por ser um <c>record</c>, a igualdade é por valor.
/// </summary>
public sealed record Uf
{
    // Sigla -> código IBGE (usado no cUF da NF-e e nos 2 primeiros dígitos da chave de acesso).
    private static readonly Dictionary<string, int> CodigosIbge = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RO"] = 11, ["AC"] = 12, ["AM"] = 13, ["RR"] = 14, ["PA"] = 15, ["AP"] = 16, ["TO"] = 17,
        ["MA"] = 21, ["PI"] = 22, ["CE"] = 23, ["RN"] = 24, ["PB"] = 25, ["PE"] = 26, ["AL"] = 27,
        ["SE"] = 28, ["BA"] = 29, ["MG"] = 31, ["ES"] = 32, ["RJ"] = 33, ["SP"] = 35, ["PR"] = 41,
        ["SC"] = 42, ["RS"] = 43, ["MS"] = 50, ["MT"] = 51, ["GO"] = 52, ["DF"] = 53,
    };

    public string Sigla { get; }
    public int CodigoIbge => CodigosIbge[Sigla];

    public Uf(string sigla)
    {
        if (string.IsNullOrWhiteSpace(sigla))
            throw new DomainException("A sigla da UF é obrigatória.");

        var normalizada = sigla.Trim().ToUpperInvariant();
        if (!CodigosIbge.ContainsKey(normalizada))
            throw new DomainException($"UF inválida: '{sigla}'.");

        Sigla = normalizada;
    }

    public static IReadOnlyList<string> Siglas { get; } = CodigosIbge.Keys.Order().ToArray();

    public static Uf DoCodigoIbge(int codigo)
    {
        var sigla = CodigosIbge.FirstOrDefault(p => p.Value == codigo).Key
                    ?? throw new DomainException($"Código IBGE de UF inválido: {codigo}.");
        return new Uf(sigla);
    }

    public override string ToString() => Sigla;
}
