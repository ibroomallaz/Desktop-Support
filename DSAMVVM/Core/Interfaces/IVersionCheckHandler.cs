namespace DSAMVVM.Core.Interfaces
{

 
    public interface IVersionCheckHandler
    {
        Task EnforceRequiredAsync(); // may block + shutdown if below required min
        Task CheckAsync();           // non-blocking update prompt if newer
    }
}
