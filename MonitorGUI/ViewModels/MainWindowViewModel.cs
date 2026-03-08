using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MonitorGUI.Models;
using MonitorGUI.Services;

namespace MonitorGUI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ProcessMonitorService _monitorService;
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private ObservableCollection<ProcessoInfo> _processos = new();

    [ObservableProperty]
    private ProcessoInfo? _processoSelecionado;

    [ObservableProperty]
    private MemoryBlockInfo? _blocoMemoriaSelecionado;

    [ObservableProperty]
    private bool _isMonitoring;

    [ObservableProperty]
    private int _intervalMs = 3000;

    [ObservableProperty]
    private string _statusMessage = "Monitor pronto.";

    [ObservableProperty]
    private string _filtroSelecionado = "TODOS";
    public string[] OpcoesFiltro { get; } = { "TODOS", "CPU-BOUND", "I/O-BOUND" };

    [ObservableProperty]
    private int _limiteSelecionado = 15;
    public int[] OpcoesLimite { get; } = { 15, 30, 50, 100, 500 };

    public MainWindowViewModel()
    {
        _monitorService = new ProcessMonitorService();
    }

    [RelayCommand]
    private void StartMonitoring()
    {
        if (IsMonitoring) return;

        IsMonitoring = true;
        StatusMessage = "Monitorando...";
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    AtualizarProcessos();
                }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(() => StatusMessage = $"Erro: {ex.Message}");
                }

                await Task.Delay(IntervalMs, token);
            }
        }, token);
    }

    [RelayCommand]
    private void StopMonitoring()
    {
        if (!IsMonitoring) return;

        _cancellationTokenSource?.Cancel();
        IsMonitoring = false;
        StatusMessage = "Monitor pausado.";
    }

    [RelayCommand]
    private void IncreaseInterval()
    {
        IntervalMs = Math.Min(IntervalMs + 500, 10000);
    }

    [RelayCommand]
    private void DecreaseInterval()
    {
        IntervalMs = Math.Max(IntervalMs - 500, 500);
    }

    [ObservableProperty]
    private ObservableCollection<ThreadInfo> _threadsSelecionadas = new();

    [ObservableProperty]
    private ObservableCollection<MemoryBlockInfo> _mapaMemoriaSimulado = new();

    partial void OnProcessoSelecionadoChanged(ProcessoInfo? value)
    {
        AtualizarThreadsNaVisao(value);
        GerarMapaMemoria(value);
    }

    [RelayCommand]
    private void SelecionarBloco(MemoryBlockInfo bloco)
    {
        if (BlocoMemoriaSelecionado != null)
            BlocoMemoriaSelecionado.IsSelected = false;

        bloco.IsSelected = true;
        BlocoMemoriaSelecionado = bloco;
    }

    private void GerarMapaMemoria(ProcessoInfo? processoAtualizado)
    {
        MapaMemoriaSimulado.Clear();
        BlocoMemoriaSelecionado = null;

        if (processoAtualizado == null || processoAtualizado.NumPaginas <= 0)
        {
            return;
        }

        // Simular um mapa de 100 blocos (cada bloco = 1% do uso virtual vs fisico, ou apenas blocos lógicos)
        // Como é uma simulação heurística do Kernel de Paginação (que espalha páginas em frames físicos reais):
        int totalBlocos = 100;
        
        // Simular que o Working Set é a parte colorida, e algumas partes estão livres/ausentes(Page Faults)
        double razaoVirtualVsFisica = 0.5; // heurística irreal apenas para efeito visual
        try 
        {
            long virt = long.Parse(processoAtualizado.MemoriaInicio.Replace("0x",""), System.Globalization.NumberStyles.HexNumber);
            if (virt > 0) razaoVirtualVsFisica = 0.8;
        } catch { }

        Random rnd = new Random(processoAtualizado.Pid); // Semente fixa pro PID
        
        for (int i = 0; i < totalBlocos; i++)
        {
            var info = new MemoryBlockInfo();
            // Determinando o tipo da página
            double sorteio = rnd.NextDouble();
            string conteudoHexBase = rnd.Next(1000000, 9999999).ToString("X");
            
            if (i < 5) 
            {
                info.CorHex = "#1976D2"; // Code Segments (.text)
                info.Tipo = "Segmento de Código (.text)";
                info.ToolTip = $"Page {i} - Block: Code / Executable";
                info.ConteudoSimulado = $"0x{conteudoHexBase}00 : 48 83 EC 28 48 8B 05 31 ...\n0x{conteudoHexBase}10 : 48 89 44 24 18 31 C0 89 ...\n0x{conteudoHexBase}20 : 0D F5 13 00 00 E8 74 F1 ...\n\n(Instruções executáveis do processo)";
            }
            else if (sorteio > razaoVirtualVsFisica)
            {
                info.CorHex = "#424242"; // Paged Out / Unmapped
                info.Tipo = "Página Ausente (Paged-Out)";
                info.ToolTip = $"Page {i} - Status: Paged Out (Disco)";
                info.ConteudoSimulado = $"(PAGE FAULT)\n\nO conteúdo deste bloco não está presente fisicamente na Memória RAM neste momento.\nEncontra-se em Swap ou arquivo de paginação do Sistema Operacional.";
            }
            else
            {
                info.CorHex = "#388E3C"; // Resident Heap / Stack in RAM
                info.Tipo = "Segmento de Dados (Heap/Stack)";
                info.ToolTip = $"Page {i} - Status: RAM Resident (Working Set)";
                
                string randData1 = rnd.Next(10, 99).ToString("X2");
                string randData2 = rnd.Next(10, 99).ToString("X2");
                info.ConteudoSimulado = $"0x{conteudoHexBase}00 : 00 00 00 00 {randData1} {randData2} 00 00\n...\nAlocação Dinâmica ou Pilha Ativa.\nPáginas presentes na contagem do Working Set físico.";
            }
            
            MapaMemoriaSimulado.Add(info);
        }
    }

    private void AtualizarThreadsNaVisao(ProcessoInfo? processoAtualizado)
    {
        ThreadsSelecionadas.Clear();
        if (processoAtualizado != null && processoAtualizado.Threads != null)
        {
            foreach (var t in processoAtualizado.Threads)
            {
                ThreadsSelecionadas.Add(t);
            }
        }
    }

    private void AtualizarProcessos()
    {
        var classificados = _monitorService.ClassificarProcessos();

        System.Collections.Generic.IEnumerable<ProcessoInfo> filtrados = classificados;

        if (FiltroSelecionado == "CPU-BOUND")
        {
            filtrados = filtrados.Where(p => p.Classificacao == "CPU-BOUND").OrderByDescending(p => p.PorcentagemCPU);
        }
        else if (FiltroSelecionado == "I/O-BOUND")
        {
            filtrados = filtrados.Where(p => p.Classificacao == "I/O-BOUND").OrderByDescending(p => p.HandleCount);
        }
        else
        {
            filtrados = filtrados.OrderByDescending(p => p.PorcentagemCPU);
        }

        var top = filtrados.Take(LimiteSelecionado).ToList();

        Dispatcher.UIThread.Post(() =>
        {
            int? idSelecionado = ProcessoSelecionado?.Pid;

            Processos.Clear();
            foreach (var p in top) Processos.Add(p);

            if (idSelecionado.HasValue)
            {
                // Tenta recuperar o processo da nova coleta e refazer a seleção, para atualizar as Threads da interface
                var novoP = Processos.FirstOrDefault(p => p.Pid == idSelecionado.Value);
                ProcessoSelecionado = novoP; 
                AtualizarThreadsNaVisao(novoP);
            }
        });
    }
}
