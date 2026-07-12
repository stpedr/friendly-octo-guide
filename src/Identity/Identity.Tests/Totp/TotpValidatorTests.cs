using Identity.Domain.Totp;
using Xunit;

namespace Identity.Tests.Totp;

public class TotpValidatorTests
{
    private static readonly byte[] Seed = System.Text.Encoding.ASCII.GetBytes("12345678901234567890");
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_111_111_111);

    [Fact]
    public void Codigo_da_janela_atual_e_valido()
    {
        var code = TotpAlgorithm.CodeAt(Seed, Now);
        var (result, step) = TotpValidator.Validate(Seed, code, Now, lastAcceptedStep: 0);

        Assert.Equal(TotpResult.Valid, result);
        Assert.Equal(TotpAlgorithm.StepOf(Now), step);
    }

    [Fact]
    public void Codigo_da_janela_anterior_e_aceito_dentro_da_tolerancia()
    {
        // Relógio do celular 30s atrás do servidor — cenário comum, não pode travar login.
        var code = TotpAlgorithm.CodeAt(Seed, Now - TimeSpan.FromSeconds(30));
        var (result, _) = TotpValidator.Validate(Seed, code, Now, lastAcceptedStep: 0);

        Assert.Equal(TotpResult.Valid, result);
    }

    [Fact]
    public void Codigo_de_duas_janelas_atras_e_recusado()
    {
        var code = TotpAlgorithm.CodeAt(Seed, Now - TimeSpan.FromSeconds(90));
        var (result, _) = TotpValidator.Validate(Seed, code, Now, lastAcceptedStep: 0);

        Assert.Equal(TotpResult.WrongCode, result);
    }

    [Fact]
    public void Mesmo_codigo_usado_duas_vezes_e_replay()
    {
        var code = TotpAlgorithm.CodeAt(Seed, Now);
        var (_, acceptedStep) = TotpValidator.Validate(Seed, code, Now, lastAcceptedStep: 0);
        var (second, _) = TotpValidator.Validate(Seed, code, Now, lastAcceptedStep: acceptedStep);

        Assert.Equal(TotpResult.Replayed, second);
    }

    [Fact]
    public void Codigo_errado_nao_avanca_o_ultimo_step_aceito()
    {
        var (result, step) = TotpValidator.Validate(Seed, "000000", Now, lastAcceptedStep: 42);

        Assert.Equal(TotpResult.WrongCode, result);
        Assert.Equal(42, step);
    }
}
