# Monitor de Processos, Threads e Memória 🖥️ (Avalonia · .NET 8)

Monitor de processos **didático e multiplataforma** (macOS, Linux e Windows), construído para ensinar os conceitos de **Sistemas Operacionais** de Tanenbaum (*Modern Operating Systems*) com **dados reais do kernel** — não com simulações disfarçadas de medição.

O projeto também serve de estudo de caso de arquitetura: **DDD** em camadas com fronteiras verificadas pelo compilador, **SOLID**, **GRASP** e Design Patterns aplicados apenas onde pagam o próprio custo.

## O que ele mostra (e de onde vem cada dado) 🔬

| Painel | Dado | Fonte real |
|---|---|---|
| Lista de processos | PID, nome, estado (modelo de Tanenbaum), **% CPU por delta entre amostras** (como top/htop), memória residente (working set), páginas no **tamanho de página real** do sistema (16 KiB em Apple Silicon), threads, handles | `System.Diagnostics` + cálculo por amostragem |
| Classificação | **CPU-bound / I-O-bound / Indeterminado**, com o critério explicado (política de bursts de Tanenbaum, trocável via Strategy) | Métricas medidas na amostra |
| Threads do processo selecionado | TID, estado (Pronta/Executando/Bloqueada/Terminada), **motivo do bloqueio**, prioridade, tempo de CPU | macOS: `ps -M` · Linux: `/proc/<pid>/task/*/stat` · Windows: `ThreadState`/`WaitReason` nativos |
| Mapa de memória | **Regiões reais** com endereços virtuais, tipo (código, heap, pilha, biblioteca…), permissões, bytes residentes vs em swap | macOS: `vmmap` · Linux: `/proc/<pid>/smaps` · Windows: `VirtualQueryEx` + `QueryWorkingSetEx` |

### Honestidade como princípio didático

- Quando o kernel **nega acesso** (processos protegidos por SIP/hardened runtime, PCB de outros usuários), isso não é engolido: aparece como 🔒 e no contador *"N processos protegidos pelo kernel"* — proteção de memória e privilégio são conteúdo da disciplina, não ruído.
- Quando o mapa real não está disponível, o app cai para um **simulador de paginação rotulado** ("MAPA SIMULADO — modo didático"), nunca para uma simulação com cara de medição.
- Quando a plataforma não expõe um dado (ex.: handles no macOS), a classificação responde **Indeterminado** em vez de chutar.

## Arquitetura 📐

```
SO.Monitor.Dominio          → nenhuma referência (o compilador prova a pureza)
SO.Monitor.Aplicacao        → Dominio
SO.Monitor.Infraestrutura   → Aplicacao, Dominio
MonitorGUI (Apresentação)   → Aplicacao, Dominio (+ Infra apenas como composition root)
SO.Monitor.Testes           → todas (xUnit)
```

- **Domínio** — o vocabulário de Tanenbaum como tipos: value objects (`Pid`, `TamanhoBytes`, `TamanhoPagina`, `EnderecoVirtual`, `FaixaDeEnderecos`), enums canônicos de estado de processo/thread, `Leitura<T>` (leitura falível que distingue "zero" de "acesso negado"), agregado `Processo`, políticas de classificação (Strategy + Chain of Responsibility) e o `SimuladorDePaginacao` (Pure Fabrication).
- **Aplicação** — casos de uso finos (`ObterAmostraClassificada`, `ObterMapaDeMemoria` com fallback rotulado) e `MonitorDeProcessos` (`PeriodicTimer` + `IAsyncEnumerable`, sem `Task.Run` solto).
- **Infraestrutura** — adapters por plataforma escolhidos uma única vez por Factory Method: parsing de `vmmap`/`ps` no macOS, leitura de `/proc` no Linux, P/Invoke Win32 no Windows; parsers puros e testados com fixtures reais.
- **Apresentação** — Avalonia 11 + MVVM (CommunityToolkit), injeção de dependência por construtor, converters (colunas ordenam pelo valor numérico), reconciliação da lista por PID (seleção e scroll sobrevivem ao ciclo).

## Executando 🛠

1. Instale o [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
2. Clone e rode:

```bash
git clone https://github.com/profraooliveira/ProcessMonitorGUI.git
cd ProcessMonitorGUI
dotnet run --project MonitorGUI/MonitorGUI.csproj
```

Testes:

```bash
dotnet test
```

*Nota (macOS/Linux):* processos do sistema são protegidos pelo kernel — o app mostra essas negações explicitamente (🔒), o que é intencional e parte da aula. Não é necessário `sudo` para monitorar os processos do seu próprio usuário.

## Autor

Mantido e desenvolvido por Prof. Raoni Oliveira ([@profraonioliveira](https://github.com/profraooliveira)).
