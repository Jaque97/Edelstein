using System.Threading.Tasks;
using Edelstein.Entities.Characters;

namespace Edelstein.Core.Gameplay.Social.Party
{
    public interface ISocialPartyManager
    {
        Task<ISocialParty> Load(int partyID);
        Task<ISocialParty?> Load(Character character);
        Task Create(Character character);
        Task Join(ISocialParty party, Character character);
        Task Disband(ISocialParty party);
        Task Withdraw(ISocialParty party, ISocialPartyMember member);
        Task Kick(ISocialParty party, ISocialPartyMember member);
        Task ChangeBoss(ISocialParty party, ISocialPartyMember member, bool disconnect = false);

        Task Chat(ISocialParty party, string name, string text);

        Task UpdateUserMigration(ISocialParty party, int characterID, int channelID, int fieldID);
        Task UpdateChangeLevelOrJob(ISocialParty party, int characterID, int level, int job);
    }
}