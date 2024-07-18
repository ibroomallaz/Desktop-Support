using System;
using System.Data.SqlTypes;
using System.Diagnostics.Tracing;
using System.Security.Policy;

/* Class to create a trigger to specify domain controller while on vpn
 * Standard general query takes an excessive amount of time while on split tunnel VPN
 * Specifying a domain controller seems to  allieviate the speed
 *
 *BC DC's: "Jolli", "Lolli", "Molli", "MONEY", "Polli" ua-dc-{}.bluecat.arizona.edu
 *10.140.81.10-15
 *
 *Not necessary while on full tunnel vpn
 */
public class VPNMode
{
    public static bool vpn = false;
    public static string Domain()
    {
        if (vpn)
        {
            Random rnd = new Random();
            
            return $"10.140.81.{rnd.Next(10,15)}"; // get specific bluecat DC address
        }

        return "bluecat.arizona.edu";
    }
}
