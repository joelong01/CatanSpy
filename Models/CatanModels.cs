using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Catan.Proxy;

namespace CatanLogSpy.Models
{
    public class AckModel
    {
        #region Properties

        public Guid AckedMessageId { get; set; }

        #endregion Properties

        #region Methods

      

        public override string ToString ()
        {
            return $"Ack={AckedMessageId}";
        }

        #endregion Methods
    }

    public class CreateGameModel 
    {
        #region Properties

        public GameInfo GameInfo { get; set; }

        #endregion Properties

        #region Methods

        public static CatanMessage CreateMessage (GameInfo gameInfo)
        {
           
            var message = new CatanMessage
            {
                ActionType = ActionType.Normal,
                Data = (object)gameInfo,
                DataTypeName = typeof(CreateGameModel).FullName,
                From = "",
                MessageId = default,
                MessageType = MessageType.BroadcastMessage,
                Sequence = 0,
                To = ""
            };
            return message;
        }

        #endregion Methods
    }
}
