namespace SO.Monitor.Dominio.Enums;

/// <summary>
/// De onde vieram os dados de um mapa de memória. Todo <c>MapaDeMemoria</c> carrega essa
/// origem explicitamente, para que a interface do usuário nunca apresente um dado simulado
/// como se fosse uma medição real do sistema operacional.
/// </summary>
public enum OrigemDosDados
{
    /// <summary>Coletada diretamente do sistema operacional.</summary>
    MedidaReal,

    /// <summary>Gerada artificialmente para fins didáticos, quando a plataforma não expõe o dado real.</summary>
    Simulada
}
