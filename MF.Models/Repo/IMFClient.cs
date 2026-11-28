using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MF.Models
{
    public interface IMFClient
    {
        public Task<string> PerformRequest(string requestJSON, string url = "", string endPoint = "", string idempotencyKey = null);
        public Task<string> PerformRequest(string requestUrl);

    }
}
