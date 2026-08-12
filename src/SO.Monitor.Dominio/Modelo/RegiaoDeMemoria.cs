using SO.Monitor.Dominio.Enums;
using SO.Monitor.Dominio.ValueObjects;

namespace SO.Monitor.Dominio.Modelo;

/// <summary>
/// Uma região contígua do espaço de endereçamento virtual de um processo, com um propósito
/// específico (código, heap, pilha, biblioteca compartilhada...) e suas próprias permissões de
/// acesso — o nível de detalhe exposto por ferramentas como /proc/&lt;pid&gt;/maps ou vmmap.
/// </summary>
public sealed record RegiaoDeMemoria(
    FaixaDeEnderecos FaixaDeEnderecos,
    TipoDeRegiao TipoDeRegiao,
    string PermissoesTexto,
    Leitura<TamanhoBytes> Residente,
    Leitura<TamanhoBytes> EmSwap,
    string? Rotulo);
