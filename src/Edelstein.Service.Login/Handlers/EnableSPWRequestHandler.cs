using System.Linq;
using System.Threading.Tasks;
using Edelstein.Core.Distributed.States;
using Edelstein.Core.Utils;
using Edelstein.Core.Utils.Packets;
using Edelstein.Entities;
using Edelstein.Entities.Characters;
using Edelstein.Network.Packets;
using Edelstein.Service.Login.Types;

namespace Edelstein.Service.Login.Handlers
{
    public class EnableSPWRequestHandler : AbstractPacketHandler<LoginServiceAdapter>
    {
        private readonly bool _vac;

        public EnableSPWRequestHandler(bool vac)
            => _vac = vac;

        protected override async Task Handle(
            LoginServiceAdapter adapter,
            RecvPacketOperations operation,
            IPacket packet
        )
        {
            packet.Decode<bool>(); // ?
            var characterID = packet.Decode<int>();

            if (_vac) packet.Decode<int>(); // Unknown

            packet.Decode<string>(); // sMacAddress
            packet.Decode<string>(); // sMacAddressWithHDDSerial
            var spw = packet.Decode<string>();

            if (adapter.Account == null) return;

            try
            {
                using var store = adapter.Service.DataStore.StartSession();
                var result = LoginResultCode.Success;

                if (_vac)
                {
                    adapter.AccountWorld = store.Query<AccountWorld>()
                        .Where(a => a.AccountID == adapter.Account.ID)
                        .Where(a => a.ID == characterID)
                        .First();
                    adapter.SelectedNode = (await adapter.Service.GetPeers())
                        .Select(n => n.State)
                        .OfType<GameServiceState>()
                        .First(s => s.Worlds.Contains(adapter.AccountWorld.WorldID));

                    if (adapter.Service.State.Worlds.All(w => w.ID != adapter.AccountWorld.WorldID))
                        result = LoginResultCode.NotConnectableWorld;
                }

                if (!string.IsNullOrEmpty(adapter.Account.SPW))
                    result = LoginResultCode.Unknown;
                if (BCrypt.Net.BCrypt.Verify(spw, adapter.Account.Password))
                    result = LoginResultCode.SamePasswordAndSPW;

                var character = store.Query<Character>()
                    .Where(c => c.AccountWorldID == adapter.AccountWorld.ID)
                    .Where(c => c.ID == characterID)
                    .First();

                if (result != LoginResultCode.Success)
                {
                    using var p = new Packet(SendPacketOperations.EnableSPWResult);

                    p.Encode<bool>(false);
                    p.Encode<byte>((byte) result);

                    await adapter.SendPacket(p);
                    return;
                }

                adapter.Character = character;
                adapter.Account.SPW = BCrypt.Net.BCrypt.HashPassword(spw);
                await adapter.TryMigrateTo(adapter.SelectedNode);
            }
            catch
            {
                using var p = new Packet(SendPacketOperations.EnableSPWResult);

                p.Encode<bool>(false);
                p.Encode<byte>((byte) LoginResultCode.Unknown);

                await adapter.SendPacket(p);
            }
        }
    }
}