using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PhysicsMeteroidsPlugin
{
	class PMNoTargetException : System.ApplicationException
	{
		public PMNoTargetException() { }
		public PMNoTargetException(string message) { }
		public PMNoTargetException(string message, System.Exception inner) { }

		protected PMNoTargetException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) { }
	
	}
}
