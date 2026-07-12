using System.Security.Cryptography;

namespace Identity.Domain.Totp;

public enum TotpResult
{
    Valid,
    WrongCode,       // código não bate em nenhuma janela aceita
    Replayed,        // código correto, mas de janela já consumida — replay de rede/shoulder-surfing
}

/// <summary>
/// Validação com tolerância de ±1 janela (relógio do celular derrapa) e proteção
/// contra replay: cada janela só autentica uma vez por usuário. O último step aceito
/// é estado do usuário (persistido junto com a seed), entra e sai por parâmetro.
/// </summary>
public static class TotpValidator
{
    public const int WindowTolerance = 1;

    public static (TotpResult Result, long AcceptedStep) Validate(
        byte[] seed, string code, DateTimeOffset now, long lastAcceptedStep)
    {
        var currentStep = TotpAlgorithm.StepOf(now);

        for (var delta = -WindowTolerance; delta <= WindowTolerance; delta++)
        {
            var step = currentStep + delta;
            var expected = TotpAlgorithm.CodeAtStep(seed, step);
            if (!FixedTimeEquals(expected, code))
                continue;

            return step <= lastAcceptedStep
                ? (TotpResult.Replayed, lastAcceptedStep)
                : (TotpResult.Valid, step);
        }

        return (TotpResult.WrongCode, lastAcceptedStep);
    }

    // Comparação em tempo constante: igualdade de string vaza timing do prefixo.
    private static bool FixedTimeEquals(string expected, string provided) =>
        expected.Length == provided.Length &&
        CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.ASCII.GetBytes(expected),
            System.Text.Encoding.ASCII.GetBytes(provided));
}
