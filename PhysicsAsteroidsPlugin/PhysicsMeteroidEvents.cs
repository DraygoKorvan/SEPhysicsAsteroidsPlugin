using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using VRageMath;

using SEModAPI.API;

namespace PhysicsMeteroidsPlugin
{
	public class EventTypeConverter : StringConverter
	{
		public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
		{
			//true means show a combobox
			return true;
		}

		public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
		{
			//true will limit to list. false will show the list, 
			//but allow free-form entry
			return true;
		}

		public override System.ComponentModel.TypeConverter.StandardValuesCollection
			   GetStandardValues(ITypeDescriptorContext context)
		{
			return new StandardValuesCollection(new string[] { "Individual", "Sector", "Location" });
		}
	}
	[Serializable()]
	public class PhysicsMeteroidEvents
	{

		//warning message
		private string m_name = "";
		private bool m_warn = false;
		private bool m_enabled = false;
		private string m_warningMessage;
		private string m_eventType = "Sector";
		private Vector3Wrapper m_location = new Vector3Wrapper(0, 0, 0);
		private int m_interval = 300;
		private int m_randInterval = 60;
		private int m_minMeteroidAmt = 1;
		private int m_maxMeteroidAmt = 10;
		private double m_oreAmt = 6000;
		private double m_largeOreAmt = 60000;

		private float m_maxVelocityFctr = 1F;
		private float m_velocityFctr = 3F;

		private UInt32 m_spawnDistance = 3000;
		private float m_spawnAcc = 3F;
		private int m_spacingTimer = 100;

		[Browsable(true)]
		public bool warn
		{
			get { return m_warn; }
			set { m_warn = value; }
		}

		[Browsable(true)]
		public string warningMessage
		{
			get { return m_warningMessage; }
			set { m_warningMessage = value; }
		}
		[Browsable(true)]
		public string name
		{
			get { return m_name; }
			set { m_name = value; }
		}
		[Browsable(true)]
		[TypeConverter(typeof(EventTypeConverter))]
		public string eventType
		{
			get { return m_eventType; }
			set
			{
				switch(value)
				{
					case "Individual":
					case "Sector":
						m_eventType = value;
						break;
					case "Location":
						if (m_eventType != value)
						{
							m_location = new Vector3Wrapper(0, 0, 0);
							m_eventType = "Location";
						}
						break;
					default:
						m_eventType = "";
						break;
				}
			}
		}

		[Browsable(true)]
		public int interval
		{
			get { return m_interval; }
			set
			{
				if (value >= 1)
				{
					m_interval = value;
					if (m_randInterval-1 > m_interval) m_randInterval = m_interval-1;
				}
			}
		}

		[Browsable(true)]
		public int randInterval
		{
			get { return m_randInterval; }
			set
			{ 
				if (value >= 0)
				{ 
					m_randInterval = value;
					if (m_randInterval - 1 > m_interval) m_randInterval = m_interval - 1;
				}
			}
		}

		[Browsable(true)]
		public int minMeteroidAmt
		{
			get { return m_minMeteroidAmt; }
			set 
			{ 
				if (value >= 0)
					if (value <= m_maxMeteroidAmt)
					{
						m_minMeteroidAmt = value;
					}
					else
					{
						m_minMeteroidAmt = m_maxMeteroidAmt;
					}
			}
		}
		[Browsable(true)]
		public int maxMeteoroidAmt
		{
			get { return m_maxMeteroidAmt; }
			set
			{
				if (value >= 0)
				{
					m_maxMeteroidAmt = value;
					if (m_maxMeteroidAmt < m_minMeteroidAmt) m_minMeteroidAmt = m_maxMeteroidAmt;
				}
			}
		}
		[Browsable(true)]
		public double oreAmt
		{
			get { return m_oreAmt; }
			set { if (value > 0.01d) m_oreAmt = value; }
		}
		[Browsable(true)]
		public double largeOreAmt
		{
			get { return m_largeOreAmt; }
			set { if (value > 1d) m_largeOreAmt = value; }
		}
		[Browsable(true)]
		public float maxVelocityFctr
		{
			get { return m_maxVelocityFctr; }
			set { if (value > 0) m_maxVelocityFctr = value; }
		}
		[Browsable(true)]
		public float velocityFctr
		{
			get { return m_velocityFctr; }
			set { if (value > 0) m_velocityFctr = value; }
		}
		[Browsable(true)]
		public UInt32 spawnDistance
		{
			get { return m_spawnDistance; }
			set { if (value >= 10) m_spawnDistance = value; }
		}
		[Browsable(true)]
		public float spawnAcc
		{
			get { return m_spawnAcc; }
			set { if (value >= 0) m_spawnAcc = value; }
		}
		[Browsable(true)]
		public int spacingTimer
		{
			get { return m_spacingTimer; }
			set { if (value > 10) m_spacingTimer = value; else m_spacingTimer = 10; }
		}
		[Browsable(true)]
		[TypeConverter(typeof(Vector3TypeConverter))]
		public Vector3Wrapper location
		{
			get { return m_location; }
			set { m_location = value; }
		}

		[Browsable(true)]
		public bool enabled
		{
			get { return m_enabled; }
			set { m_enabled = value; }
		}
		[Browsable(true)]
		[ReadOnly(true)]
		public DateTime lastrun
		{
			get;
			set;
		}

		[Browsable(true)]
		[ReadOnly(true)]
		public DateTime nextrun
		{
			get;
			set;
		}

		
		#region methods

		//use a more friendly name
		public override string ToString()
		{
			return m_name + " " + m_eventType + " " + m_interval.ToString();
		}

		#endregion
	}
}
