using ZeusFiscalMonitor.Domain.Exceptions;

namespace ZeusFiscalMonitor.Domain.ValueObjects;

/// <summary>
/// Chave de acesso de 44 dígitos do DF-e:
/// cUF(2) AAMM(4) CNPJ(14) mod(2) serie(3) nNF(9) tpEmis(1) cNF(8) cDV(1).
/// Valida formato e dígito verificador (módulo 11).
/// </summary>
public sealed record ChaveAcesso
{
    public const int Tamanho = 44;

    public string Valor { get; }

    public ChaveAcesso(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new DomainException("A chave de acesso é obrigatória.");

        var limpa = valor.Trim();
        if (limpa.Length != Tamanho || !limpa.All(char.IsAsciiDigit))
            throw new DomainException($"A chave de acesso deve conter exatamente {Tamanho} dígitos numéricos.");

        if (CalcularDigitoVerificador(limpa[..43]) != limpa[43] - '0')
            throw new DomainException("Dígito verificador da chave de acesso inválido.");

        Valor = limpa;
    }

    public int CodigoUf => int.Parse(Valor[..2]);
    public string CodigoModelo => Valor.Substring(20, 2);

    /// <summary>Módulo 11 com pesos de 2 a 9, da direita para a esquerda (Manual de Orientação do Contribuinte).</summary>
    public static int CalcularDigitoVerificador(string chaveSemDv)
    {
        var soma = 0;
        var peso = 2;
        for (var i = chaveSemDv.Length - 1; i >= 0; i--)
        {
            soma += (chaveSemDv[i] - '0') * peso;
            peso = peso == 9 ? 2 : peso + 1;
        }

        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    public override string ToString() => Valor;
}
