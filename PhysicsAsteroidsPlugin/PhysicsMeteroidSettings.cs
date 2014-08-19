using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;


namespace PhysicsMeteroidsPlugin
{
	[Serializable()]
	public class PhysicsMeteroidSettings
	{

		private bool m_meteorOn = true;


		private List<PhysicsMeteroidEvents> m_events = new List<PhysicsMeteroidEvents>();


		public bool meteorOn
		{
			get { return m_meteorOn; }
			set { m_meteorOn = value; }
		}
		[Browsable(true)]
		[ReadOnly(false)]
		public List<PhysicsMeteroidEvents> events
		{
			get { return m_events; }
			set { m_events = value; }
		}
	}
}
