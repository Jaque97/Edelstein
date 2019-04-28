using System;
using System.Linq;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using Edelstein.Core;
using Edelstein.Core.Distributed.Migrations;
using Edelstein.Core.Distributed.Peers.Info;
using Edelstein.Database.Entities;
using Edelstein.Network.Packets;
using Edelstein.Provider.Templates.Field;
using Edelstein.Service.Game.Fields.User;
using Edelstein.Service.Game.Logging;
using Foundatio.Caching;

namespace Edelstein.Service.Game.Services
{
    public partial class GameSocket : AbstractMigrateableSocket<GameServiceInfo>
    {
        private static readonly ILog Logger = LogProvider.GetCurrentClassLogger();

        public GameService Service { get; }

        public Account Account { get; set; }
        public AccountData AccountData { get; set; }
        public Character Character { get; set; }
        public FieldUser FieldUser { get; set; }

        public GameSocket(
            IChannel channel,
            uint seqSend,
            uint seqRecv,
            GameService service
        ) : base(channel, seqSend, seqRecv, service)
        {
            Service = service;
        }

        public override Task OnPacket(IPacket packet)
        {
            var operation = (RecvPacketOperations) packet.Decode<short>();
            return operation switch {
                RecvPacketOperations.MigrateIn => OnMigrateIn(packet),
                RecvPacketOperations.AliveAck => TryProcessHeartbeat(Account, Character),
                _ => FieldUser?.OnPacket(operation, packet)
                };
        }

        public override Task OnException(Exception exception)
        {
            Logger.Error(exception, "Socket caught exception");
            return Task.CompletedTask;
        }

        public override async Task OnUpdate()
        {
            using (var store = Service.DataStore.OpenSession())
            {
                Character.FieldPortal = (byte) FieldUser.Field.Template.Portals
                    .Values
                    .Where(p => p.Type == FieldPortalType.StartPoint)
                    .OrderBy(p =>
                    {
                        var xd = p.Position.X - FieldUser.Position.X;
                        var yd = p.Position.Y - FieldUser.Position.Y;

                        return xd * xd + yd * yd;
                    })
                    .First()
                    .ID;

                await store.UpdateAsync(Account);
                await store.UpdateAsync(AccountData);
                await store.UpdateAsync(Character);
            }
        }

        public override async Task OnDisconnect()
        {
            if (FieldUser != null) await FieldUser.Field.Leave(FieldUser);
            if (Account == null) return;

            var state = (await Service.AccountStateCache.GetAsync<MigrationState>(Account.ID.ToString())).Value;

            if (state != MigrationState.Migrating)
            {
                await OnUpdate();
                await Service.AccountStateCache.RemoveAsync(Account.ID.ToString());
            }
        }
    }
}