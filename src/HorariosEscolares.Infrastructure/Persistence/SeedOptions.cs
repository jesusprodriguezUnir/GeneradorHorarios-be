namespace HorariosEscolares.Infrastructure.Persistence;

public class SeedOptions
{
    public int Levels { get; set; } = 6;
    public int LinesPerLevel { get; set; } = 3;
    public int? InfantilLines { get; set; }
    public int? PrimariaLines { get; set; }
    public int? SecundariaLines { get; set; }
    public string Modality { get; set; } = "estandar";
    public string ScheduleType { get; set; } = "continua";
}
