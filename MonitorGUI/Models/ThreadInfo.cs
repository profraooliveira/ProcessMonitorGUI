namespace MonitorGUI.Models;

public class ThreadInfo
{
    public int Id { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string WaitReason { get; set; } = string.Empty;
    public string Prioridade { get; set; } = string.Empty;
}
