using Edelstein.Core.Distributed.States;
using Edelstein.Core.Services.Migrations;
using Edelstein.Core.Utils;
using Edelstein.Core.Utils.Messaging;
using Edelstein.Database;
using Edelstein.Network;
using Edelstein.Service.Login.Handlers;
using Foundatio.Caching;
using Foundatio.Lock;
using Microsoft.Extensions.Options;

namespace Edelstein.Service.Login
{
    public class LoginService : AbstractMigrationService<LoginServiceState>
    {
        public const string AuthLockKey = "lock:auth";
        public ILockProvider LockProvider { get; }

        public LoginService(
            IOptions<LoginServiceState> state,
            IDataStore dataStore,
            ICacheClient cache,
            IMessageBusFactory busFactory,
            ILockProvider lockProvider
        ) : base(state.Value, dataStore, cache, busFactory)
        {
            LockProvider = lockProvider;

            Handlers[RecvPacketOperations.CheckPassword] = new CheckPasswordHandler();
            Handlers[RecvPacketOperations.SetGender] = new SetGenderHandler();
        }

        public override ISocketAdapter Build(ISocket socket)
            => new LoginServiceAdapter(socket, this);
    }
}