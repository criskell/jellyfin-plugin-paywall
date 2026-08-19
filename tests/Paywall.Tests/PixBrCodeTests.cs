using System.Text;
using Paywall.Infrastructure.Providers;
using Xunit;

namespace Paywall.Tests;

public class PixBrCodeTests
{
    private static string Build(string key, decimal amount, string? name = "Loja Exemplo", string? city = "São Paulo") =>
        PixBrCode.Build(new PixBrCodeRequest(key, amount, "pedido123")
        {
            PayeeName = name,
            PayeeCity = city
        });

    [Fact]
    public void PayloadSegueAOrdemEOsValoresDoBrCode()
    {
        var fields = Tlv.Parse(Build("chave@exemplo.com", 19.90m));

        Assert.Equal("01", fields["00"]);
        Assert.Equal("0000", fields["52"]);
        Assert.Equal("986", fields["53"]);
        Assert.Equal("19.90", fields["54"]);
        Assert.Equal("BR", fields["58"]);
    }

    [Fact]
    public void ContaDoRecebedorTrazOIdentificadorDoPixEAChave()
    {
        var account = Tlv.Parse(Tlv.Parse(Build("chave@exemplo.com", 19.90m))["26"]);

        Assert.Equal("BR.GOV.BCB.PIX", account["00"]);
        Assert.Equal("chave@exemplo.com", account["01"]);
    }

    [Fact]
    public void IdentificadorDaTransacaoVaiNoCampoAdicional()
    {
        var additional = Tlv.Parse(Tlv.Parse(Build("chave@exemplo.com", 19.90m))["62"]);

        Assert.Equal("PEDIDO123", additional["05"]);
    }

    [Fact]
    public void AcentoESinalSaemDoNomeEDaCidade()
    {
        var fields = Tlv.Parse(Build("chave@exemplo.com", 19.90m, "Ação & Mídia", "São Paulo"));

        Assert.Equal("ACAO MIDIA", fields["59"]);
        Assert.Equal("SAO PAULO", fields["60"]);
    }

    [Fact]
    public void NomeLongoECortadoNoLimiteDoPadrao()
    {
        var fields = Tlv.Parse(Build("chave@exemplo.com", 19.90m, new string('A', 40)));

        Assert.Equal(25, fields["59"].Length);
    }

    [Fact]
    public void NomeVazioViraUmValorPadraoEmVezDeCampoInvalido()
    {
        var fields = Tlv.Parse(Build("chave@exemplo.com", 19.90m, name: "   "));

        Assert.Equal("RECEBEDOR", fields["59"]);
    }

    [Fact]
    public void ValorUsaPontoIndependenteDaCulturaLocal()
    {
        Assert.Equal("1234.50", Tlv.Parse(Build("chave@exemplo.com", 1234.50m))["54"]);
    }

    [Fact]
    public void CodigoTerminaComVerificadorValido()
    {
        var payload = Build("chave@exemplo.com", 19.90m);

        Assert.True(Crc16Ccitt.IsWellFormed(payload));
    }

    [Fact]
    public void QualquerAlteracaoNoCodigoInvalidaOVerificador()
    {
        var payload = Build("chave@exemplo.com", 19.90m);
        var adulterado = payload.Replace("19.90", "99.90");

        Assert.False(Crc16Ccitt.IsWellFormed(adulterado));
    }

    [Fact]
    public void ChaveAlemDoLimiteEhRecusadaEmVezDeGerarCodigoQuebrado()
    {
        Assert.Throws<ArgumentException>(() => Build(new string('k', 120), 19.90m));
    }

    private static class Tlv
    {
        public static Dictionary<string, string> Parse(string payload)
        {
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            var position = 0;

            while (position + 4 <= payload.Length)
            {
                var id = payload.Substring(position, 2);
                var length = int.Parse(payload.Substring(position + 2, 2));

                fields[id] = payload.Substring(position + 4, length);
                position += 4 + length;
            }

            return fields;
        }
    }

    private static class Crc16Ccitt
    {
        public static bool IsWellFormed(string payload)
        {
            var separator = payload.LastIndexOf("6304", StringComparison.Ordinal);

            if (separator < 0 || separator + 8 != payload.Length)
            {
                return false;
            }

            var expected = Of(payload[..(separator + 4)]);

            return string.Equals(payload[(separator + 4)..], expected, StringComparison.Ordinal);
        }

        public static string Of(string text)
        {
            var crc = (ushort)0xFFFF;

            foreach (var octet in Encoding.ASCII.GetBytes(text))
            {
                crc ^= (ushort)(octet << 8);

                for (var bit = 0; bit < 8; bit++)
                {
                    crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
                }
            }

            return crc.ToString("X4");
        }
    }

    [Fact]
    public void OVerificadorDoTesteBateComOValorPublicadoDoAlgoritmo()
    {
        Assert.Equal("29B1", Crc16Ccitt.Of("123456789"));
    }
}
