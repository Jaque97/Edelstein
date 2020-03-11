using Edelstein.Network.Packets;
using Edelstein.Service.Game.Conversations.Speakers;

namespace Edelstein.Service.Game.Conversations.Requests
{
    public class AskNumberRequest : AbstractConversationRequest<int>
    {
        public override ConversationRequestType Type => ConversationRequestType.AskNumber;
        private readonly string _text;
        private readonly int _def;
        private readonly int _min;
        private readonly int _max;

        public AskNumberRequest(
            IConversationSpeaker speaker,
            string text,
            int def,
            int min,
            int max
        ) : base(speaker)
        {
            _text = text;
            _def = def;
            _min = min;
            _max = max;
        }

        public override bool Validate(IConversationResponse<int> response)
            => response.Value >= _min &&
               response.Value <= _max;

        public override void EncodeData(IPacket packet)
        {
            packet.Encode<string>(_text);
            packet.Encode<int>(_def);
            packet.Encode<int>(_min);
            packet.Encode<int>(_max);
        }
    }
}