using System.Threading.Tasks;
using Edelstein.Entities.Characters;
using Edelstein.Network.Packets;

namespace Edelstein.Service.Game.Fields.Objects
{
    public interface IFieldUser : IFieldLife
    {
        GameService Service { get; }
        GameServiceAdapter Adapter { get; }
        Character Character { get; }
        bool IsInstantiated { get; set; }

        IPacket GetSetFieldPacket();

        Task SendPacket(IPacket packet);
        Task BroadcastPacket(IPacket packet);
    }
}