public class Mod
{
  public string Id { get; set; }
  public string Name { get; set; }
  public string DisplayName { get; set; }
  public string[] Author { get; set; }
  public string Description { get; set; }
  public string Prefix { get; set; }
  public string MainFile { get; set; }
  public int Priority { get; set; }
  public string BadgeColour { get; set; }
  public string BadgeTextColour { get; set; }
  public string Version { get; set; }
  public List<Dependency> Dependencies { get; set; }
  public List<Conflict> Conflicts { get; set; }
  public List<string> Provides { get; set; }
  public bool DumpLoc { get; set; }
  public string Status { get; set; }
  public bool IsInstalled { get; set; }
}
