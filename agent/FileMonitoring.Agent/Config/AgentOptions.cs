namespace FileMonitoring.Agent.Config;

public class AppConfig
{
    public string Name { get; set; } = "";
    public string[] ProcessNames { get; set; } = [];
    public bool Enabled { get; set; } = true;
}

public class AgentOptions
{
    public string ServerUrl { get; set; } = "";
    public string EnrollmentToken { get; set; } = "";
    public bool AllowInsecureHttp { get; set; }
    public string DataDir { get; set; } = @"C:\ProgramData\FileMonitoringAgent";
    public double MinConfidence { get; set; } = 0.6;
    public List<AppConfig> Applications { get; set; } = [];
}
