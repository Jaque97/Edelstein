using Edelstein.Database.Entities.Characters;
using Marten.Schema;

namespace Edelstein.Database.Entities
{
    public class FriendRecord
    {
        public int ID { get; set; }

        [ForeignKey(typeof(Character))] public int CharacterID { get; set; }
        [ForeignKey(typeof(Character))] public int PairCharacterID { get; set; }

        public string PairCharacterName { get; set; }
        public long SN { get; set; }
        public long PairSN { get; set; }
        public int FriendItemID { get; set; }
    }
}