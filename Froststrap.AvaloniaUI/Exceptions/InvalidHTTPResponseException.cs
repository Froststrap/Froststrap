using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Froststrap.Exceptions
{
    internal class InvalidHTTPResponseException : Exception
    {
        public InvalidHTTPResponseException(string message) : base(message) { }
    }
}
