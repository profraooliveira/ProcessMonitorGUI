using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MonitorGUI.Models;

public class ProcessoInfo
{
    // Ignorando System.Diagnostics.Process diretamente na Model para a View, 
    // mas vamos manter os IDs e Nomes para simplificar.
    public int Pid { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string Caminho { get; set; } = string.Empty;

    public string StatusResposta { get; set; } = string.Empty;
    public string PrioridadeBase { get; set; } = string.Empty;
    public string ClassePrioridade { get; set; } = string.Empty;

    public string TempoInicio { get; set; } = string.Empty;
    public TimeSpan TempoCPU { get; set; }
    public TimeSpan TempoExecucao { get; set; }
    public double PorcentagemCPU { get; set; }

    public string WorkingSetFormatado { get; set; } = string.Empty;
    public string VirtualMemoryFormatado { get; set; } = string.Empty;
    public string PrivateMemoryFormatado { get; set; } = string.Empty;

    public string MemoriaInicio { get; set; } = string.Empty;
    public string MemoriaFim { get; set; } = string.Empty;
    public long NumPaginas { get; set; }

    public int HandleCount { get; set; }
    public int ThreadCount { get; set; }

    public string Classificacao { get; set; } = string.Empty;
    public string ExplicacaoClassificacao { get; set; } = string.Empty;

    public List<ThreadInfo> Threads { get; set; } = new();
}
