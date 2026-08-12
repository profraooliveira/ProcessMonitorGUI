using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using SO.Monitor.Aplicacao.Ports;
using SO.Monitor.Dominio;
using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.Modelo;
using SO.Monitor.Dominio.ValueObjects;

namespace SO.Monitor.Infraestrutura.Windows;

/// <summary>
/// Fonte real de mapa de memória no Windows, via <c>VirtualQueryEx</c> (<see cref="NativoWin32"/>).
/// Percorre o espaço de endereçamento do processo alvo região por região, do endereço 0 até
/// <c>MaximumApplicationAddress</c> (obtido de <c>GetSystemInfo</c>), classificando cada uma com
/// <see cref="ClassificadorDeRegiaoWindows"/>. Para regiões efetivamente comprometidas (COMMIT),
/// mede a residência real por amostragem de páginas via <c>QueryWorkingSetEx</c>.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FonteDeMapaDeMemoriaWindows : IFonteDeMapaDeMemoria
{
    /// <summary>
    /// Teto de páginas efetivamente consultadas por região via <c>QueryWorkingSetEx</c>. Contar
    /// página a página uma região gigante (ex.: um mapeamento de vários GB) seria caro; amostrar
    /// um número fixo de páginas, distribuídas uniformemente pela região, e extrapolar a fração
    /// residente é o mesmo compromisso que ferramentas reais (ex.: VMMap) fazem.
    /// </summary>
    private const int MaximoDePaginasAmostradas = 256;

    private const long TamanhoDePagina = 4096;

    /// <summary>
    /// Percorrer o espaço de endereçamento inteiro via <c>VirtualQueryEx</c> região por região é
    /// trabalho síncrono e pode ser sensivelmente lento (processos com milhares de regiões); por
    /// isso a varredura roda no thread pool via <see cref="Task.Run(Func{Task},CancellationToken)"/>
    /// em vez de inline na thread chamadora — que, sendo esta uma fonte consumida a partir do
    /// <c>MainWindowViewModel</c>, normalmente é a UI.
    /// </summary>
    public ValueTask<Leitura<MapaDeMemoria>> ObterMapaAsync(Pid pid, CancellationToken cancellationToken)
    {
        // Guarda honesta: a FabricaDeFontes só instancia esta classe em Windows, então este ramo
        // nunca deveria ser exercitado em produção — mas devolver "não suportado" em vez de deixar
        // o P/Invoke falhar de forma obscura é o comportamento correto se isso um dia mudar.
        if (!OperatingSystem.IsWindows())
            return ValueTask.FromResult(Leitura<MapaDeMemoria>.NaoSuportada());

        return new ValueTask<Leitura<MapaDeMemoria>>(Task.Run(() => ObterMapa(pid, cancellationToken), cancellationToken));
    }

    private static Leitura<MapaDeMemoria> ObterMapa(Pid pid, CancellationToken cancellationToken)
    {
        var handleDoProcesso = NativoWin32.OpenProcess(
            NativoWin32.PROCESS_QUERY_INFORMATION | NativoWin32.PROCESS_VM_READ,
            bInheritHandle: false,
            (uint)pid.Valor);

        if (handleDoProcesso == IntPtr.Zero)
            return Leitura<MapaDeMemoria>.Negada();

        try
        {
            NativoWin32.GetSystemInfo(out var infoDoSistema);

            var regioes = new List<RegiaoDeMemoria>();
            var enderecoAtual = (nuint)0;
            var enderecoMaximo = infoDoSistema.MaximumApplicationAddress;
            var tamanhoDaEstrutura = (nuint)Marshal.SizeOf<NativoWin32.MEMORY_BASIC_INFORMATION>();

            while (enderecoAtual < enderecoMaximo)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var bytesEscritos = NativoWin32.VirtualQueryEx(handleDoProcesso, enderecoAtual, out var info, tamanhoDaEstrutura);
                if (bytesEscritos == 0)
                    break; // VirtualQueryEx não conseguiu descrever mais nada a partir daqui

                if (info.RegionSize == 0)
                    break; // segurança: nunca avançar por uma região de tamanho zero

                var (tipo, permissoes, rotulo) = ClassificadorDeRegiaoWindows.Classificar(info.Type, info.State, info.Protect);

                var faixa = new FaixaDeEnderecos(
                    new EnderecoVirtual(info.BaseAddress),
                    new EnderecoVirtual(info.BaseAddress + info.RegionSize));

                var residente = info.State == ClassificadorDeRegiaoWindows.MEM_COMMIT
                    ? MedirResidencia(handleDoProcesso, info)
                    : Leitura<TamanhoBytes>.NaoSuportada();

                // O Win32 não expõe, por região e de forma barata, quanto está em pagefile — só
                // contadores agregados do processo inteiro. Preferimos admitir "não suportado" a
                // inventar um número.
                var emSwap = Leitura<TamanhoBytes>.NaoSuportada();

                regioes.Add(new RegiaoDeMemoria(faixa, tipo, permissoes, residente, emSwap, rotulo));

                var proximoEndereco = info.BaseAddress + info.RegionSize;
                if (proximoEndereco <= enderecoAtual)
                    break; // segurança extra contra loop infinito por região degenerada

                enderecoAtual = proximoEndereco;
            }

            var mapa = new MapaDeMemoria(pid, OrigemDosDados.MedidaReal, regioes, DateTimeOffset.UtcNow);
            return Leitura<MapaDeMemoria>.Ok(mapa);
        }
        finally
        {
            NativoWin32.CloseHandle(handleDoProcesso);
        }
    }

    /// <summary>
    /// Mede a residência real de uma região COMMIT via <c>QueryWorkingSetEx</c>, amostrando no
    /// máximo <see cref="MaximoDePaginasAmostradas"/> páginas distribuídas uniformemente pela
    /// região e extrapolando: <c>Residente = fração_residente_amostrada × extensão</c>. Para
    /// regiões pequenas (poucas páginas), a amostra cobre a região inteira, então o resultado é
    /// exato, não estimado.
    /// </summary>
    private static Leitura<TamanhoBytes> MedirResidencia(IntPtr handleDoProcesso, NativoWin32.MEMORY_BASIC_INFORMATION info)
    {
        var extensaoDaRegiao = (long)info.RegionSize;
        var totalDePaginas = extensaoDaRegiao / TamanhoDePagina;
        if (totalDePaginas <= 0)
            return Leitura<TamanhoBytes>.Ok(TamanhoBytes.Zero);

        var paginasAmostradas = (int)Math.Min(totalDePaginas, MaximoDePaginasAmostradas);
        var passo = Math.Max(1L, totalDePaginas / paginasAmostradas);
        var enderecoBase = (long)info.BaseAddress;

        var entradas = new NativoWin32.PSAPI_WORKING_SET_EX_INFORMATION[paginasAmostradas];
        for (var i = 0; i < paginasAmostradas; i++)
        {
            var indiceDaPagina = Math.Min(i * passo, totalDePaginas - 1);
            entradas[i].VirtualAddress = (nuint)(enderecoBase + indiceDaPagina * TamanhoDePagina);
        }

        var tamanhoEmBytes = (uint)(entradas.Length * Marshal.SizeOf<NativoWin32.PSAPI_WORKING_SET_EX_INFORMATION>());
        if (!NativoWin32.QueryWorkingSetEx(handleDoProcesso, entradas, tamanhoEmBytes))
            return Leitura<TamanhoBytes>.NaoSuportada();

        var residentesNaAmostra = entradas.Count(entrada => (entrada.VirtualAttributes & NativoWin32.WorkingSetExValidBit) != 0);
        var fracaoResidente = (double)residentesNaAmostra / entradas.Length;
        var bytesResidentesEstimados = (long)(fracaoResidente * extensaoDaRegiao);

        return Leitura<TamanhoBytes>.Ok(new TamanhoBytes(bytesResidentesEstimados));
    }
}
