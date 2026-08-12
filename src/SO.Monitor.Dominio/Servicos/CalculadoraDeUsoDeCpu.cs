using SO.Monitor.Dominio.ValueObjects;

namespace SO.Monitor.Dominio.Servicos;

/// <summary>
/// Calcula a utilização de CPU de um processo entre duas amostras, da mesma forma que
/// top/htop: o delta de tempo de CPU consumido dividido pelo intervalo decorrido e pelo
/// número de núcleos lógicos — utilização real do instante, não a média de vida inteira do processo.
/// </summary>
public static class CalculadoraDeUsoDeCpu
{
    /// <summary>
    /// Calcula o percentual de uso de CPU no intervalo entre duas amostras.
    /// Retorna zero quando o intervalo é zero ou negativo, ou quando o delta de CPU é negativo
    /// — sintoma de que o contador de tempo de CPU do processo voltou a zero (processo reiniciado
    /// ou PID reaproveitado pelo SO).
    /// </summary>
    public static Percentual Calcular(
        TimeSpan cpuAnterior,
        TimeSpan cpuAtual,
        TimeSpan intervaloDecorrido,
        int nucleosLogicos)
    {
        if (intervaloDecorrido <= TimeSpan.Zero || nucleosLogicos <= 0)
            return Percentual.Zero;

        var deltaCpu = cpuAtual - cpuAnterior;
        if (deltaCpu < TimeSpan.Zero)
            return Percentual.Zero;

        var utilizacao = deltaCpu.TotalMilliseconds / (intervaloDecorrido.TotalMilliseconds * nucleosLogicos);
        return new Percentual(utilizacao * 100.0);
    }
}
