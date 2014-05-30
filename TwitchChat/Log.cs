using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchChat
{
    class Log
    {
        public static Log Instance = new Log();

        private Log()
        {
        }



        internal void BeginReconnect()
        {
            throw new NotImplementedException();
        }

        internal void EndReconnect()
        {
            throw new NotImplementedException();
        }

        internal void WebApiError(string url, string p)
        {
        }
    }
}
