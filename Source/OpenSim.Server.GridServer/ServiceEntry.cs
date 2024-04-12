
namespace OpenSim.Server.GridServer
{
    public partial class GridServer
    {
        public struct ServiceEntry 
        {
            public ServiceEntry(uint port, string module) { Port = port; ModuleName = module; }
            public ServiceEntry(string entry)
            {
                string[] split = entry.Split(new char[] { ':' });
                Port = Convert.ToUInt32(split[0]);
                ModuleName = string.IsNullOrEmpty(split[1]) ? string.Empty : split[1];               
            }

            public uint Port { get; }
            public string ModuleName { get; }
        }
    }
}