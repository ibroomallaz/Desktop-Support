using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;


public class Departmentlist
{
    public Department[] Department { get; set; }
}

public class Department
{
    public string Number { get; set; }
    public bool SupportKnown { get; set; }
    public bool SplitSupport { get; set; }
    public Team[]? Team1 { get; set; }
    public Team[]? Team2 { get; set; }
    public Filerepo[]? filerepo { get; set; }
    public string? notes { get; set; }
}

public class Team
{
    public string Name { get; set; }
    public bool ServiceNow { get; set; }
}

public class Filerepo
{
    public bool Exists { get; set; }
    public string Location { get; set; }
}
