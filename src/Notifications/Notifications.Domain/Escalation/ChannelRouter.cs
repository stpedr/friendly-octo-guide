namespace Notifications.Domain.Escalation;

public enum Channel { Push, Email }

/// <summary>
/// Severidade decide o canal: crítico acorda gente (push + e-mail de trilha),
/// warning vai de push, info só entra no digest de e-mail — ruído de madrugada
/// é o jeito mais rápido de treinar on-call a ignorar alerta.
/// </summary>
public static class ChannelRouter
{
    public static IReadOnlyList<Channel> ChannelsFor(Severity severity) => severity switch
    {
        Severity.Critical => [Channel.Push, Channel.Email],
        Severity.Warning => [Channel.Push],
        _ => [Channel.Email],
    };
}
