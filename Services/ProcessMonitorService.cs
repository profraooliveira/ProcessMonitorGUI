using System;
using System.Collections.Generic;
using System.Diagnostics;
using MonitorGUI.Models;

namespace MonitorGUI.Services;

public class ProcessMonitorService
{
    public List<ProcessoInfo> ClassificarProcessos()
    {
        var lista = new List<ProcessoInfo>();
        var processos = Process.GetProcesses();

        foreach (var p in processos)
        {
            try
            {
                var info = new ProcessoInfo
                {
                    Pid = p.Id,
                    Nome = p.ProcessName
                };

                info.SessionId = GetSessionIdSafe(p).ToString();
                info.Caminho = GetCaminhoSafe(p);
                info.StatusResposta = GetRespondingSafe(p);
                info.PrioridadeBase = GetPrioridadeBaseSafe(p);
                info.ClassePrioridade = GetClassePrioridadeSafe(p);
                info.TempoInicio = GetTempoInicioSafe(p);

                // Obter métricas
                TimeSpan tempoCPU = TimeSpan.Zero;
                TimeSpan tempoExecucao = TimeSpan.Zero;
                int handleCount = 0;

                try { tempoCPU = p.TotalProcessorTime; } catch { }
                try { tempoExecucao = DateTime.Now - p.StartTime; } catch { }
                try { handleCount = p.HandleCount; } catch { }

                info.TempoCPU = tempoCPU;
                info.TempoExecucao = tempoExecucao;
                info.HandleCount = handleCount;

                long workingSet = GetWorkingSetSafe(p);
                long virtualMem = GetVirtualMemorySafe(p);
                long privateMem = GetPrivateMemorySafe(p);

                // Cálculo Hexadecimal dos Endereços Base Virtuais
                info.MemoriaInicio = $"0x{virtualMem:X12}";
                info.MemoriaFim = $"0x{(virtualMem + workingSet):X12}";
                info.NumPaginas = workingSet / 4096;

                info.WorkingSetFormatado = FormatarBytes(workingSet);
                info.VirtualMemoryFormatado = FormatarBytes(virtualMem);
                info.PrivateMemoryFormatado = FormatarBytes(privateMem);
                info.ThreadCount = GetThreadCountSafe(p);

                // Calcular porcentagem de uso de CPU
                double porcentagemCPU = 0;
                if (tempoExecucao.TotalMilliseconds > 0)
                {
                    porcentagemCPU = (tempoCPU.TotalMilliseconds / tempoExecucao.TotalMilliseconds) * 100;
                }
                info.PorcentagemCPU = Math.Round(porcentagemCPU, 4);

                // Classificar o processo
                if (porcentagemCPU > 5 || tempoCPU.TotalSeconds > 60)
                {
                    info.Classificacao = "CPU-BOUND";
                    info.ExplicacaoClassificacao = $"CPU: {info.PorcentagemCPU:F2}% do tempo | Cálculos intensivos";
                }
                else if (handleCount > 50 || porcentagemCPU < 1)
                {
                    info.Classificacao = "I/O-BOUND";
                    info.ExplicacaoClassificacao = $"Handles: {handleCount} | Aguarda I/O frequentemente";
                }
                else
                {
                    if (handleCount > 20)
                    {
                        info.Classificacao = "I/O-BOUND";
                        info.ExplicacaoClassificacao = $"Handles: {handleCount} | Operações de I/O moderadas";
                    }
                    else
                    {
                        info.Classificacao = "CPU-BOUND";
                        info.ExplicacaoClassificacao = $"CPU: {info.PorcentagemCPU:F2}% | Baixo I/O";
                    }
                }

                // Extrair threads
                var threadsList = new List<ThreadInfo>();
                try
                {
                    foreach (ProcessThread t in p.Threads)
                    {
                        var tInfo = new ThreadInfo
                        {
                            Id = t.Id,
                            Estado = t.ThreadState.ToString()
                        };
                        
                        try { tInfo.Prioridade = t.BasePriority.ToString(); } catch { }
                        
                        if (t.ThreadState == System.Diagnostics.ThreadState.Wait)
                        {
                            try 
                            { 
                                tInfo.WaitReason = t.WaitReason.ToString(); 
                            } 
                            catch (PlatformNotSupportedException) 
                            { 
                                tInfo.WaitReason = "OS não suporta ler Motivo (Unix)"; 
                            }
                            catch (Exception)
                            {
                                tInfo.WaitReason = "Sem permissão";
                            }
                        }
                        else
                        {
                            tInfo.WaitReason = "-";
                        }
                        
                        threadsList.Add(tInfo);
                    }
                }
                catch { }

                info.Threads = threadsList;

                lista.Add(info);
            }
            catch
            {
                // Ignorar processos que não temos acesso
            }
            finally
            {
                p.Dispose();
            }
        }

        return lista;
    }

    private string GetCaminhoSafe(Process p)
    {
        try
        {
            string path = p.MainModule?.FileName ?? "N/A";
            return path;
        }
        catch { return "N/A (acesso negado)"; }
    }

    private int GetSessionIdSafe(Process p)
    {
        try { return p.SessionId; }
        catch { return -1; }
    }

    private string GetRespondingSafe(Process p)
    {
        try { return p.Responding ? "Respondendo" : "NÃO RESPONDE"; }
        catch { return "N/A"; }
    }

    private string GetPrioridadeBaseSafe(Process p)
    {
        try { return p.BasePriority.ToString(); }
        catch { return "N/A"; }
    }

    private string GetClassePrioridadeSafe(Process p)
    {
        try { return p.PriorityClass.ToString(); }
        catch { return "N/A"; }
    }

    private string GetTempoInicioSafe(Process p)
    {
        try { return p.StartTime.ToString("dd/MM/yyyy HH:mm:ss"); }
        catch { return "N/A"; }
    }

    private long GetWorkingSetSafe(Process p) { try { return p.WorkingSet64; } catch { return 0; } }
    private long GetVirtualMemorySafe(Process p) { try { return p.VirtualMemorySize64; } catch { return 0; } }
    private long GetPrivateMemorySafe(Process p) { try { return p.PrivateMemorySize64; } catch { return 0; } }
    private int GetThreadCountSafe(Process p) { try { return p.Threads.Count; } catch { return 0; } }

    private string FormatarBytes(long bytes)
    {
        string[] sufixos = { "B", "KB", "MB", "GB", "TB" };
        int indice = 0;
        double tamanho = bytes;

        while (tamanho >= 1024 && indice < sufixos.Length - 1)
        {
            tamanho /= 1024;
            indice++;
        }

        return $"{tamanho:F2} {sufixos[indice]}";
    }
}
