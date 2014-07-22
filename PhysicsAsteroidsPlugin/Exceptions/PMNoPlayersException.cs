using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PhysicsMeteroidsPlugin
{
	class PMNoPlayersException : System.ApplicationException
	{
		public PMNoPlayersException() { }
		public PMNoPlayersException(string message) { }
		public PMNoPlayersException(string message, System.Exception inner) { }

		protected PMNoPlayersException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) { }
	}
}
