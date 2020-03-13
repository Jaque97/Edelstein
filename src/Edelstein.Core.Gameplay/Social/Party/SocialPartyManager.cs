using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Edelstein.Core.Distributed;
using Edelstein.Core.Gameplay.Migrations;
using Edelstein.Core.Gameplay.Social.Party.Events;
using Edelstein.Database;
using Edelstein.Entities.Characters;
using Edelstein.Entities.Social;
using Foundatio.Caching;
using Foundatio.Lock;

namespace Edelstein.Core.Gameplay.Social.Party
{
    public class SocialPartyManager : ISocialPartyManager
    {
        private const string PartyLockKey = "lock:party";

        private readonly int _channelID;
        private readonly INode _node;
        private readonly IDataStore _store;
        private readonly ILockProvider _lockProvider;
        private readonly ICacheClient _characterCache;

        public SocialPartyManager(
            int channelID,
            INode node,
            IDataStore store,
            ILockProvider lockProvider,
            ICacheClient cache
        )
        {
            _channelID = channelID;
            _node = node;
            _store = store;
            _lockProvider = lockProvider;
            _characterCache = new ScopedCacheClient(cache, MigrationScopes.StateCharacter);
        }

        private async Task Lock(Func<Task> func)
        {
            var token = new CancellationTokenSource();

            token.CancelAfter(TimeSpan.FromSeconds(5));

            var @lock =
                await _lockProvider.AcquireAsync(
                    PartyLockKey,
                    cancellationToken: token.Token
                );

            if (@lock == null)
                throw new PartyException("Request timed out");

            try
            {
                await func.Invoke();
            }
            catch (Exception e)
            {
                throw new PartyException(e.Message);
            }
            finally
            {
                await @lock.ReleaseAsync();
            }
        }

        private async Task<T> Lock<T>(Func<Task<T>> func)
        {
            var token = new CancellationTokenSource();

            token.CancelAfter(TimeSpan.FromSeconds(5));

            var @lock =
                await _lockProvider.AcquireAsync(
                    PartyLockKey,
                    cancellationToken: token.Token
                );

            if (@lock == null)
                throw new PartyException("Request timed out");

            try
            {
                return await func.Invoke();
            }
            catch (Exception e)
            {
                throw new PartyException(e.Message);
            }
            finally
            {
                await @lock.ReleaseAsync();
            }
        }

        private Task<ISocialParty?> LoadInner(Character character)
        {
            using var store = _store.StartSession();
            var member = store
                .Query<PartyMember>()
                .Where(m => m.CharacterID == character.ID)
                .FirstOrDefault();

            if (member == null) return Task.FromResult<ISocialParty?>(null);

            var record = store
                .Query<Entities.Social.Party>()
                .Where(p => p.ID == member.PartyID)
                .FirstOrDefault();
            var members = store
                .Query<PartyMember>()
                .Where(m => m.PartyID == record.ID)
                .ToImmutableList();

            return Task.FromResult<ISocialParty?>(new SocialParty(record, members));
        }

        private async Task<ISocialParty> CreateInner(Character character)
        {
            using var store = _store.StartSession();
            var member = store
                .Query<PartyMember>()
                .Where(m => m.CharacterID == character.ID)
                .FirstOrDefault();

            if (member != null)
                throw new PartyException("Creating party when character already in party");
            var record = new Entities.Social.Party
            {
                BossCharacterID = character.ID
            };

            await store.InsertAsync(record);

            member = new PartyMember
            {
                PartyID = record.ID,
                CharacterID = character.ID,
                CharacterName = character.Name,
                Job = character.Job,
                Level = character.Level,
                ChannelID = _channelID,
                FieldID = character.FieldID
            };

            await store.InsertAsync(member);

            var members = store
                .Query<PartyMember>()
                .Where(m => m.PartyID == record.ID)
                .ToImmutableList();

            return new SocialParty(record, members);
        }

        private async Task<ISocialParty> JoinInner(ISocialParty party, Character character)
        {
            using var store = _store.StartSession();
            var record = store
                .Query<Entities.Social.Party>()
                .Where(p => p.ID == party.ID)
                .FirstOrDefault();
            var members = store
                .Query<PartyMember>()
                .Where(m => m.PartyID == record.ID)
                .ToImmutableList();

            if (record == null)
                throw new PartyException("Joining non-existent party");
            if (members.Any(m => m.ChannelID == character.ID))
                throw new PartyException("Joining already joined party");

            var member = new PartyMember
            {
                CharacterID = character.ID,
                CharacterName = character.Name,
                Job = character.Job,
                Level = character.Level,
                ChannelID = _channelID
            };

            await store.InsertAsync(member);
            return await LoadInner(character);
        }

        private Task DisbandInner(ISocialParty party)
        {
            throw new System.NotImplementedException();
        }

        private Task WithdrawInner(ISocialParty party, ISocialPartyMember member)
        {
            throw new System.NotImplementedException();
        }

        private Task KickInner(ISocialParty party, ISocialPartyMember member)
        {
            throw new System.NotImplementedException();
        }

        private Task ChangeBossInner(ISocialParty party, ISocialPartyMember member)
        {
            throw new System.NotImplementedException();
        }

        private async Task UpdateUserMigrationInner(ISocialParty party, int characterID, int channelID, int fieldID)
        {
            using var store = _store.StartSession();
            var member = store
                .Query<PartyMember>()
                .Where(m => m.CharacterID == characterID)
                .FirstOrDefault();

            if (member == null) return;

            member.ChannelID = channelID;
            member.FieldID = fieldID;

            store.Update(member);

            await BroadcastMessage(party, new PartyUserMigrationEvent(
                party.ID,
                characterID,
                channelID,
                fieldID
            ));
        }

        public async Task UpdateChangeLevelOrJobInner(ISocialParty party, int characterID, int level, int job)
        {
            using var store = _store.StartSession();
            var member = store
                .Query<PartyMember>()
                .Where(m => m.CharacterID == characterID)
                .FirstOrDefault();

            if (member == null) return;

            member.Level = level;
            member.Job = job;

            store.Update(member);

            await BroadcastMessage(party, new PartyChangeLevelOrJobEvent(
                party.ID,
                characterID,
                level,
                job
            ));
        }

        private async Task BroadcastMessage<T>(ISocialParty party, T message) where T : class
        {
            var targets = (await _characterCache.GetAllAsync<INodeState>(
                    party.Members.Select(m => m.CharacterID.ToString())
                )).Values
                .Where(t => t.HasValue)
                .Select(t => t.Value.Name)
                .Distinct();

            await Task.WhenAll((await _node.GetPeers())
                .Where(p => targets.Contains(p.State.Name))
                .Select(p => p.SendMessage<T>(message)));
        }

        public Task<ISocialParty?> Load(Character character)
            => Lock<ISocialParty?>(() => LoadInner(character));

        public Task<ISocialParty> Create(Character character)
            => Lock<ISocialParty?>(() => CreateInner(character));

        public Task<ISocialParty> Join(ISocialParty party, Character character)
            => Lock<ISocialParty>(() => JoinInner(party, character));

        public Task Disband(ISocialParty party)
            => Lock(() => DisbandInner(party));

        public Task Withdraw(ISocialParty party, ISocialPartyMember member)
            => Lock(() => WithdrawInner(party, member));

        public Task Kick(ISocialParty party, ISocialPartyMember member)
            => Lock(() => KickInner(party, member));

        public Task ChangeBoss(ISocialParty party, ISocialPartyMember member)
            => Lock(() => ChangeBossInner(party, member));

        public Task UpdateUserMigration(ISocialParty party, int characterID, int channelID, int fieldID)
            => Lock(() => UpdateUserMigrationInner(party, characterID, channelID, fieldID));

        public Task UpdateChangeLevelOrJob(ISocialParty party, int characterID, int level, int job)
            => Lock(() => UpdateChangeLevelOrJobInner(party, characterID, level, job));
    }
}