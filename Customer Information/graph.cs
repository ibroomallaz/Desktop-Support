using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.Graph;
public static class Graph
{
    public static GraphServiceClient InitializeGraphClient()
    {
        var credential = new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions
        {
            ClientId = Globals.g_ClientID,
            RedirectUri = new Uri("http://localhost"),
            TenantId = Globals.g_TenantID
        });

        return new GraphServiceClient(credential);
    }

    public static async Task<bool> TestGraphConnectionAsync(GraphServiceClient client)
    {
        try
        {
            var me = await client.Me.GetAsync();
            Console.WriteLine($"Authenticated as: {me.DisplayName}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Graph error: {ex.Message}");
            return false;
        }
    }
}
public class EntraDevice
{
    public string Name { get; set; }
    public bool? Exists { get; set; }
    public List<string> Groups { get; set; } = new();
}
