using System.Diagnostics;

class MonitorProcessos
{
    // Estrutura para armazenar informações do processo com classificação
    class ProcessoInfo
    {
        public Process Processo { get; set; } = null!;
        public double PorcentagemCPU { get; set; }
        public string Classificacao { get; set; } = "";
        public string ExplicacaoClassificacao { get; set; } = "";
        public int HandleCount { get; set; }
        public TimeSpan TempoCPU { get; set; }
        public TimeSpan TempoExecucao { get; set; }
    }

    static void Main()
    {
        Console.CursorVisible = false;

        // Exibir legenda inicial
        ExibirLegendaInicial();
        Console.WriteLine("\nPressione qualquer tecla para iniciar o monitor...");
        Console.ReadKey(true);

        int intervaloMs = 3000;
        bool pausado = false;
        bool executando = true;
        bool mostrarLegenda = false;

        while (executando)
        {
            if (Console.KeyAvailable)
            {
                var tecla = Console.ReadKey(true).Key;
                switch (tecla)
                {
                    case ConsoleKey.Q:
                        executando = false;
                        continue;
                    case ConsoleKey.P:
                        pausado = !pausado;
                        break;
                    case ConsoleKey.L:
                        mostrarLegenda = !mostrarLegenda;
                        break;
                    case ConsoleKey.Add:
                    case ConsoleKey.OemPlus:
                        intervaloMs = Math.Min(intervaloMs + 500, 10000);
                        break;
                    case ConsoleKey.Subtract:
                    case ConsoleKey.OemMinus:
                        intervaloMs = Math.Max(intervaloMs - 500, 500);
                        break;
                }
            }

            if (pausado)
            {
                Thread.Sleep(100);
                continue;
            }

            Console.Clear();

            if (mostrarLegenda)
            {
                ExibirLegendaInicial();
                Console.WriteLine("\n[Pressione L para voltar ao monitor]");
                Thread.Sleep(500);
                continue;
            }

            // Cabeçalho
            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.DarkBlue;
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║           MONITOR DE PROCESSOS - PCB COMPLETO COM CLASSIFICAÇÃO CPU/IO BOUND                      ║");
            Console.WriteLine($"║  {DateTime.Now:HH:mm:ss} | Intervalo: {intervaloMs}ms | [Q]Sair [P]Pausar [L]Legenda [+/-]Intervalo                       ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();

            // Coletar e classificar processos
            var processosClassificados = ClassificarProcessos();

            // Separar em CPU-bound e I/O-bound
            var cpuBound = processosClassificados
                .Where(p => p.Classificacao == "CPU-BOUND")
                .OrderByDescending(p => p.PorcentagemCPU)
                .Take(10)
                .ToList();

            var ioBound = processosClassificados
                .Where(p => p.Classificacao == "I/O-BOUND")
                .OrderByDescending(p => p.HandleCount)
                .Take(10)
                .ToList();

            // Exibir seção CPU-BOUND
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  🔥 PROCESSOS CPU-BOUND (10 principais) - Alto uso de processador, cálculos intensivos            ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();

            foreach (var info in cpuBound)
            {
                ExibirProcessoCompleto(info);
            }

            // Exibir seção I/O-BOUND
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  💾 PROCESSOS I/O-BOUND (10 principais) - Alto uso de entrada/saída, espera por dispositivos      ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();

            foreach (var info in ioBound)
            {
                ExibirProcessoCompleto(info);
            }

            // Limpar recursos
            foreach (var info in processosClassificados)
            {
                info.Processo.Dispose();
            }

            Thread.Sleep(intervaloMs);
        }

        Console.CursorVisible = true;
        Console.Clear();
        Console.WriteLine("Monitor encerrado.");
    }

    static void ExibirLegendaInicial()
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                           GUIA DE CONCEITOS - SISTEMAS OPERACIONAIS                               ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════════════════════════════════════════╝");
        Console.ResetColor();

        // CPU-BOUND vs I/O-BOUND
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n═══════════════════════════════════════════════════════════════════════════════════════════════════════");
        Console.WriteLine("                              CPU-BOUND vs I/O-BOUND");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════════════════════════════");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("\n🔥 CPU-BOUND (Limitado por CPU):");
        Console.ResetColor();
        Console.WriteLine("   • Processos que passam a MAIOR PARTE do tempo executando instruções na CPU");
        Console.WriteLine("   • Realizam cálculos intensivos, processamento de dados, renderização");
        Console.WriteLine("   • Exemplos: compiladores, codificação de vídeo, cálculos científicos, jogos");
        Console.WriteLine("   • Característica: Alto tempo de CPU em relação ao tempo total de execução");
        Console.WriteLine("   • Métrica: (Tempo CPU / Tempo Execução) > 50%");
        Console.WriteLine("   • Escalonamento ideal: Time-slice maior para evitar overhead de context switch");

        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine("\n💾 I/O-BOUND (Limitado por Entrada/Saída):");
        Console.ResetColor();
        Console.WriteLine("   • Processos que passam a MAIOR PARTE do tempo ESPERANDO operações de I/O");
        Console.WriteLine("   • Aguardam disco, rede, teclado, mouse, impressora, etc.");
        Console.WriteLine("   • Exemplos: editores de texto, navegadores, servidores web, bancos de dados");
        Console.WriteLine("   • Característica: Muitos handles abertos, baixo uso de CPU");
        Console.WriteLine("   • Métrica: (Tempo CPU / Tempo Execução) < 20% OU HandleCount > 100");
        Console.WriteLine("   • Escalonamento ideal: Alta prioridade para responder rápido quando I/O completa");

        // PRIORIDADES
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n═══════════════════════════════════════════════════════════════════════════════════════════════════════");
        Console.WriteLine("                              SISTEMA DE PRIORIDADES");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════════════════════════════");
        Console.ResetColor();

        Console.WriteLine("\n┌─────────────────────┬────────┬────────────────────────────┬────────────────────────────────────┐");
        Console.WriteLine("│ Classe de Prioridade│ Valor  │ ESCALA VISUAL (0-31)       │ Descrição                          │");
        Console.WriteLine("├─────────────────────┼────────┼────────────────────────────┼────────────────────────────────────┤");
        Console.ForegroundColor = ConsoleColor.DarkRed;
        Console.WriteLine("│ RealTime            │   24   │ ████████████████████████░░ │ ⚠️ CRÍTICO! Pode travar sistema    │");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("│ High                │   13   │ █████████████░░░░░░░░░░░░░ │ 🔴 Tarefas críticas                │");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("│ AboveNormal         │   10   │ ██████████░░░░░░░░░░░░░░░░ │ 🟠 Acima do normal                 │");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("│ Normal              │    8   │ ████████░░░░░░░░░░░░░░░░░░ │ 🟢 Padrão (maioria)                │");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("│ BelowNormal         │    6   │ ██████░░░░░░░░░░░░░░░░░░░░ │ 🔵 Segundo plano                   │");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine("│ Idle                │    4   │ ████░░░░░░░░░░░░░░░░░░░░░░ │ ⚪ Apenas quando ocioso            │");
        Console.ResetColor();
        Console.WriteLine("└─────────────────────┴────────┴────────────────────────────┴────────────────────────────────────┘");

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("\n   ESCALA DE PRIORIDADE (Windows/macOS):");
        Console.WriteLine("   ╔════════════════════════════════════════════════════════════════════════════════════════════╗");
        Console.Write("   ║ ");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write("0    ");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("4    ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("6    8    ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("10   ");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("13        ");
        Console.ForegroundColor = ConsoleColor.DarkRed;
        Console.Write("24                    ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("31 ║");
        Console.Write("   ║ ");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write("│    ");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("│    ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("│    │    ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("│    ");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("│         ");
        Console.ForegroundColor = ConsoleColor.DarkRed;
        Console.Write("│                     ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("│  ║");
        Console.WriteLine("   ║ ├────┼────┼────┼────┼────┼─────────┼─────────────────────┤  ║");
        Console.Write("   ║ ");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write("IDLE ");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("BELOW");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("    NORMAL   ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("ABOVE");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("   HIGH      ");
        Console.ForegroundColor = ConsoleColor.DarkRed;
        Console.Write("REALTIME              ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("   ║");
        Console.WriteLine("   ╠════════════════════════════════════════════════════════════════════════════════════════════╣");
        Console.WriteLine("   ║  ◀─────── MENOR PRIORIDADE ───────────────────────── MAIOR PRIORIDADE ───────────────────▶ ║");
        Console.WriteLine("   ╚════════════════════════════════════════════════════════════════════════════════════════════╝");

        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("\n📊 Como funciona o escalonamento por prioridade:");
        Console.ResetColor();
        Console.WriteLine("   1. O SO mantém múltiplas filas, uma para cada nível de prioridade");
        Console.WriteLine("   2. Processos de maior prioridade são executados primeiro (preemptivo)");
        Console.WriteLine("   3. Dentro da mesma prioridade: Round-Robin com quantum de tempo");
        Console.WriteLine("   4. Prioridade dinâmica: SO pode aumentar prioridade de processos I/O-bound");
        Console.WriteLine("      para melhorar responsividade (priority boost)");
        Console.WriteLine("   5. Aging: Processos esperando muito tempo ganham prioridade temporária");
        Console.WriteLine("      para evitar starvation (inanição)");

        // PCB
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n═══════════════════════════════════════════════════════════════════════════════════════════════════════");
        Console.WriteLine("                              PROCESS CONTROL BLOCK (PCB)");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════════════════════════════");
        Console.ResetColor();
        Console.WriteLine("\nO PCB é a estrutura de dados que o SO usa para gerenciar cada processo:");
        Console.WriteLine("   [1] Identificação: PID, nome, caminho, sessão");
        Console.WriteLine("   [2] Estado: Running, Ready, Waiting, Blocked, Terminated");
        Console.WriteLine("   [3] CPU/Scheduling: Prioridade, tempo de CPU, registradores salvos");
        Console.WriteLine("   [4] Memória: Working set, memória virtual, páginas alocadas");
        Console.WriteLine("   [5] Paginação: Tabela de páginas, endereços virtuais/físicos");
        Console.WriteLine("   [6] I/O: Handles abertos, dispositivos em uso");
        Console.WriteLine("   [7] Threads: Lista de threads do processo");
        Console.WriteLine("   [8] Classificação: CPU-bound ou I/O-bound (calculado em tempo real)");
    }

    static List<ProcessoInfo> ClassificarProcessos()
    {
        var lista = new List<ProcessoInfo>();
        var processos = Process.GetProcesses();

        foreach (var p in processos)
        {
            try
            {
                var info = new ProcessoInfo { Processo = p };

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

                // Calcular porcentagem de uso de CPU
                double porcentagemCPU = 0;
                if (tempoExecucao.TotalMilliseconds > 0)
                {
                    porcentagemCPU = (tempoCPU.TotalMilliseconds / tempoExecucao.TotalMilliseconds) * 100;
                }
                info.PorcentagemCPU = porcentagemCPU;

                // Classificar o processo
                // CPU-BOUND: Alta porcentagem de tempo de CPU
                // I/O-BOUND: Baixa porcentagem de CPU ou muitos handles (indica muitas operações I/O)
                if (porcentagemCPU > 5 || tempoCPU.TotalSeconds > 60)
                {
                    info.Classificacao = "CPU-BOUND";
                    info.ExplicacaoClassificacao = $"CPU: {porcentagemCPU:F2}% do tempo | Cálculos intensivos";
                }
                else if (handleCount > 50 || porcentagemCPU < 1)
                {
                    info.Classificacao = "I/O-BOUND";
                    info.ExplicacaoClassificacao = $"Handles: {handleCount} | Aguarda I/O frequentemente";
                }
                else
                {
                    // Classificar baseado em heurística
                    if (handleCount > 20)
                    {
                        info.Classificacao = "I/O-BOUND";
                        info.ExplicacaoClassificacao = $"Handles: {handleCount} | Operações de I/O moderadas";
                    }
                    else
                    {
                        info.Classificacao = "CPU-BOUND";
                        info.ExplicacaoClassificacao = $"CPU: {porcentagemCPU:F2}% | Baixo I/O";
                    }
                }

                lista.Add(info);
            }
            catch
            {
                p.Dispose();
            }
        }

        return lista;
    }

    static void ExibirProcessoCompleto(ProcessoInfo info)
    {
        var p = info.Processo;

        // Cor baseada na classificação
        Console.ForegroundColor = info.Classificacao == "CPU-BOUND" ? ConsoleColor.Red : ConsoleColor.Blue;
        Console.WriteLine($"┌───────────────────────────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine($"│ ▶ PROCESSO: {p.ProcessName,-25} │ PID: {p.Id,-8} │ {info.Classificacao,-10}                      │");
        Console.WriteLine($"├───────────────────────────────────────────────────────────────────────────────────────────────────┤");
        Console.ResetColor();

        try
        {
            // [1] IDENTIFICAÇÃO
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"│ [1] IDENTIFICAÇÃO                                                                                 │");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"│   PID: {p.Id,-12} Nome: {p.ProcessName,-30} Session: {GetSessionIdSafe(p),-10}           │");

            string caminho = GetCaminhoSafe(p);
            Console.WriteLine($"│   Caminho: {caminho,-84}│");

            // [2] ESTADO E PRIORIDADE (com explicação e escala visual)
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"│ [2] ESTADO E PRIORIDADE                                                                           │");
            Console.ForegroundColor = ConsoleColor.White;

            string responding = GetRespondingSafe(p);
            string prioridadeBaseStr = GetPrioridadeBaseSafe(p);
            string classePrioridade = GetClassePrioridadeSafe(p);
            string explicacaoPrioridade = ExplicarPrioridade(classePrioridade);

            int prioridadeBaseInt = 8; // default Normal
            int.TryParse(prioridadeBaseStr, out prioridadeBaseInt);

            Console.WriteLine($"│   Status: {responding,-15} Prioridade Base: {prioridadeBaseStr,-5} Classe: {classePrioridade,-15}              │");
            Console.WriteLine($"│   → {explicacaoPrioridade,-92}│");

            // Escala visual de prioridade
            string escalaVisual = GerarEscalaPrioridade(prioridadeBaseInt);
            Console.Write($"│   Escala: [");
            Console.ForegroundColor = GetCorPrioridade(prioridadeBaseInt);
            Console.Write(escalaVisual);
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"] 0{new string('-', 10)}8{new string('-', 10)}24{new string('-', 4)}31              │");
            Console.WriteLine($"│           IDLE──BELOW──NORMAL──ABOVE──HIGH────────REALTIME                                        │");

            // [3] CLASSIFICAÇÃO CPU/IO BOUND (com explicação detalhada)
            Console.ForegroundColor = info.Classificacao == "CPU-BOUND" ? ConsoleColor.Red : ConsoleColor.Blue;
            Console.WriteLine($"│ [3] CLASSIFICAÇÃO: {info.Classificacao,-78}│");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"│   {info.ExplicacaoClassificacao,-94}│");

            string comportamento = info.Classificacao == "CPU-BOUND"
                ? "Processo usa CPU intensivamente. Ideal: quantum maior, menor preempção."
                : "Processo espera I/O frequentemente. Ideal: prioridade maior para resposta rápida.";
            Console.WriteLine($"│   Comportamento: {comportamento,-79}│");

            // [4] CPU / SCHEDULING
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"│ [4] CPU / SCHEDULING                                                                              │");
            Console.ForegroundColor = ConsoleColor.White;

            string tempoInicio = GetTempoInicioSafe(p);
            string tempoCPU = info.TempoCPU.ToString(@"hh\:mm\:ss\.fff");
            string tempoExec = info.TempoExecucao.ToString(@"dd\.hh\:mm\:ss");

            Console.WriteLine($"│   Início: {tempoInicio,-20} Execução: {tempoExec,-15} CPU Total: {tempoCPU,-18}│");
            Console.WriteLine($"│   % CPU: {info.PorcentagemCPU:F4}%   (Tempo CPU / Tempo Execução × 100)                                       │");

            // [5] MEMÓRIA E PAGINAÇÃO
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"│ [5] MEMÓRIA E PAGINAÇÃO                                                                           │");
            Console.ForegroundColor = ConsoleColor.White;

            long workingSet = p.WorkingSet64;
            long virtualMem = p.VirtualMemorySize64;
            long privateMem = p.PrivateMemorySize64;
            const long tamPagina = 4096;
            long numPaginas = workingSet / tamPagina;

            Console.WriteLine($"│   Working Set: {FormatarBytes(workingSet),-12} Virtual: {FormatarBytes(virtualMem),-12} Privada: {FormatarBytes(privateMem),-15}   │");
            Console.WriteLine($"│   Páginas (4KB): {numPaginas,-10} Início: 0x{virtualMem:X12}  Fim: 0x{virtualMem + workingSet:X12}       │");

            // [6] I/O
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"│ [6] I/O                                                                                           │");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"│   Handles: {info.HandleCount,-10} (Arquivos, sockets, pipes, etc. abertos pelo processo)                   │");

            // [7] THREADS
            int threadCount = p.Threads.Count;
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"│ [7] THREADS ({threadCount} total)                                                                          │");
            Console.ForegroundColor = ConsoleColor.White;

            int count = 0;
            foreach (ProcessThread t in p.Threads)
            {
                if (count >= 3) break;
                try
                {
                    string estado = t.ThreadState.ToString();
                    string prio = t.BasePriority.ToString();
                    string waitReason = "";
                    if (t.ThreadState == System.Diagnostics.ThreadState.Wait)
                    {
                        try { waitReason = $"({t.WaitReason})"; } catch { }
                    }
                    Console.WriteLine($"│   TID: {t.Id,-8} Estado: {estado,-10} {waitReason,-15} Prioridade: {prio,-5}                       │");
                }
                catch
                {
                    Console.WriteLine($"│   TID: {t.Id,-8} (sem acesso)                                                                     │");
                }
                count++;
            }
            if (threadCount > 3)
            {
                Console.WriteLine($"│   ... +{threadCount - 3} threads                                                                              │");
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"│ ERRO: {ex.Message[..Math.Min(ex.Message.Length, 80)],-90}│");
        }

        Console.ForegroundColor = info.Classificacao == "CPU-BOUND" ? ConsoleColor.Red : ConsoleColor.Blue;
        Console.WriteLine($"└───────────────────────────────────────────────────────────────────────────────────────────────────┘");
        Console.ResetColor();
    }

    static string ExplicarPrioridade(string classe)
    {
        return classe switch
        {
            "RealTime" => "⚠️  TEMPO REAL: Prioridade máxima, pode prejudicar o sistema!",
            "High" => "🔴 ALTA: Executa antes de processos normais, tarefas críticas",
            "AboveNormal" => "🟠 ACIMA DO NORMAL: Mais importante que aplicações comuns",
            "Normal" => "🟢 NORMAL: Prioridade padrão para maioria das aplicações",
            "BelowNormal" => "🔵 ABAIXO DO NORMAL: Tarefas em segundo plano",
            "Idle" => "⚪ OCIOSA: Executa apenas quando CPU não tem mais nada",
            _ => "Prioridade não identificada"
        };
    }

    static string GerarEscalaPrioridade(int prioridadeBase)
    {
        // Escala de 0 a 31, com 25 caracteres de largura
        const int larguraEscala = 25;
        int posicao = (int)((prioridadeBase / 31.0) * larguraEscala);
        posicao = Math.Clamp(posicao, 0, larguraEscala - 1);

        char[] escala = new char[larguraEscala];
        for (int i = 0; i < larguraEscala; i++)
        {
            if (i < posicao)
                escala[i] = '█';
            else if (i == posicao)
                escala[i] = '▓';
            else
                escala[i] = '░';
        }

        return new string(escala);
    }

    static ConsoleColor GetCorPrioridade(int prioridadeBase)
    {
        return prioridadeBase switch
        {
            >= 24 => ConsoleColor.DarkRed,
            >= 13 => ConsoleColor.Red,
            >= 10 => ConsoleColor.Yellow,
            >= 8 => ConsoleColor.Green,
            >= 6 => ConsoleColor.Cyan,
            _ => ConsoleColor.Gray
        };
    }

    // Métodos auxiliares para obter informações com segurança
    static string GetCaminhoSafe(Process p)
    {
        try
        {
            string path = p.MainModule?.FileName ?? "N/A";
            return path.Length > 84 ? "..." + path[^81..] : path;
        }
        catch { return "N/A (acesso negado)"; }
    }

    static int GetSessionIdSafe(Process p)
    {
        try { return p.SessionId; }
        catch { return -1; }
    }

    static string GetRespondingSafe(Process p)
    {
        try { return p.Responding ? "Respondendo" : "NÃO RESPONDE"; }
        catch { return "N/A"; }
    }

    static string GetPrioridadeBaseSafe(Process p)
    {
        try { return p.BasePriority.ToString(); }
        catch { return "N/A"; }
    }

    static string GetClassePrioridadeSafe(Process p)
    {
        try { return p.PriorityClass.ToString(); }
        catch { return "N/A"; }
    }

    static string GetTempoInicioSafe(Process p)
    {
        try { return p.StartTime.ToString("dd/MM/yyyy HH:mm:ss"); }
        catch { return "N/A"; }
    }

    static string FormatarBytes(long bytes)
    {
        string[] sufixos = ["B", "KB", "MB", "GB", "TB"];
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
