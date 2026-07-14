using Platform.ServiceDefaults.Security;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Platform.Tests.Security;

public class PiiMaskerTests
{
    [Theory]
    [InlineData("operador@fabrica.com", "o*******@fabrica.com")]
    [InlineData("a@x.com", "*@x.com")]
    [InlineData("sem-arroba", "s*********")]
    [InlineData("", "***")]
    public void Email_mascara_local_e_preserva_dominio(string input, string expected)
        => Assert.Equal(expected, PiiMasker.MaskEmail(input));

    [Theory]
    [InlineData("123.456.789-00", 4, "**********9-00")]
    [InlineData("998877", 4, "**8877")]
    [InlineData("12", 4, "**")]
    public void Tail_preserva_os_ultimos_digitos(string input, int keep, string expected)
        => Assert.Equal(expected, PiiMasker.Tail(input, keep));

    [Fact]
    public void Mask_generico_esconde_tudo_menos_o_primeiro()
        => Assert.Equal("s*****", PiiMasker.Mask("segred"));
}

public class PgCryptoTests
{
    [Fact]
    public void Encrypt_e_decrypt_geram_expressao_parametrizada()
    {
        Assert.Equal("pgp_sym_encrypt($1, $2)", PgCrypto.EncryptExpr("$1", "$2"));
        Assert.Equal("pgp_sym_decrypt(cpf_enc, @key)", PgCrypto.DecryptExpr("cpf_enc", "@key"));
    }

    [Fact]
    public void Chave_literal_e_recusada_para_nao_virar_injecao()
        => Assert.Throws<ArgumentException>(() => PgCrypto.EncryptExpr("$1", "'chave-em-claro'"));
}

public class PiiMaskingEnricherTests
{
    private sealed class Factory : Serilog.Core.ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false)
            => new(name, new ScalarValue(value));
    }

    private static LogEvent EventWith(params (string Name, object Value)[] props)
    {
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow, LogEventLevel.Information, exception: null,
            new MessageTemplate("m", []),
            props.Select(p => new LogEventProperty(p.Name, new ScalarValue(p.Value))));
        return logEvent;
    }

    [Fact]
    public void Enricher_mascara_email_e_senha_mas_nao_campos_neutros()
    {
        var logEvent = EventWith(("email", "op@fab.com"), ("senha", "hunter2"), ("linha", "2"));

        PiiMaskingEnricher.Default().Enrich(logEvent, new Factory());

        Assert.Equal("o*@fab.com", ((ScalarValue)logEvent.Properties["email"]).Value);
        Assert.Equal("h******", ((ScalarValue)logEvent.Properties["senha"]).Value);
        Assert.Equal("2", ((ScalarValue)logEvent.Properties["linha"]).Value); // campo neutro intacto
    }

    [Fact]
    public void Enricher_ignora_propriedade_nao_string()
    {
        var logEvent = EventWith(("password", 12345)); // número, não string
        PiiMaskingEnricher.Default().Enrich(logEvent, new Factory());
        Assert.Equal(12345, ((ScalarValue)logEvent.Properties["password"]).Value); // não explode
    }
}
