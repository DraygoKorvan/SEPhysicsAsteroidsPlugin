using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Timers;
using System.Reflection;
using System.Threading;

using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.VRageData;

using SEModAPIExtensions.API.Plugin;
using SEModAPIExtensions.API.Plugin.Events;

using SEModAPIInternal.API.Common;
using SEModAPIInternal.API.Entity;
using SEModAPIInternal.API.Entity.Sector.SectorObject;
using SEModAPIInternal.API.Entity.Sector.SectorObject.CubeGrid;
using SEModAPIInternal.API.Entity.Sector.SectorObject.CubeGrid.CubeBlock;
using SEModAPIInternal.API.Server;
using SEModAPIInternal.Support;

using SEModAPI.API;

using VRageMath;
using VRage.Common.Utils;


namespace PhysicsAsteroidsPlugin
{
	public class Core : PluginBase
	{
		#region "Attributes"

		private double m_ore_amt = 60000;
		private double m_ore_fctr = 1;
		private float m_maxVelocityFctr = 1F;
		private float m_velocityFctr = 3F;
		private bool m_running = false;
		private bool m_meteoron = true;

		private static Type m_InventoryItemType = new MyObjectBuilderType(typeof(MyObjectBuilder_InventoryItem));
		private static Type m_OreType = new MyObjectBuilderType(typeof(MyObjectBuilder_Ore));
		private static Type m_FloatingObjectType = new MyObjectBuilderType(typeof(MyObjectBuilder_FloatingObject));

		private Random m_gen;

		#endregion

		#region "Constructors and Initializers"

		public Core()
		{
			Console.WriteLine("PhysicsAsteroidPlugin '" + Id.ToString() + "' constructed!");
		}

		public override void Init()
		{
			m_running = false;
			m_gen = new Random(3425325);//temp hash
			m_maxVelocityFctr = 1F;
			m_velocityFctr = 3F;

			Console.WriteLine("PhysicsAsteroidPlugin '" + Id.ToString() + "' initialized!");
		}

		#endregion

		#region "Properties"

		[Category("Physics Meteor Plugin")]
		[Browsable(true)]
		[ReadOnly(false)]
		public double ore_fctr
		{
			get { return m_ore_fctr; }
			set { m_ore_fctr = value; }
		}

		[Category("Physics Meteor Plugin")]
		[Browsable(true)]
		[ReadOnly(false)]
		public double ore_amt
		{
			get { return m_ore_amt; }
			set { if (value > 0) m_ore_amt = value; }
		}

		[Category("Physics Meteor Plugin")]
		[Browsable(true)]
		[ReadOnly(false)]
		public float MaxVelocityFctr
		{
			get { return m_maxVelocityFctr; }
			set { m_maxVelocityFctr = value; }
		}

		[Category("Physics Meteor Plugin")]
		[Browsable(true)]
		[ReadOnly(false)]
		public float velocityFctr
		{
			get { return m_velocityFctr; }
			set { m_velocityFctr = value; }
		}

		[Category("Physics Meteor Plugin")]
		[Browsable(true)]
		[ReadOnly(false)]
		public bool meteoron
		{
			get { return m_meteoron; }
			set { if (value) m_meteoron = true; else m_meteoron = false; }
		}
		#endregion

		#region "Methods"

		public static void velocityloop(FloatingObject obj)
		{
			Thread.Sleep(10);
			for (int count = 20; count > 0; count--)
			{
				if (obj.Mass > 0)
				{
					obj.LinearVelocity = obj.LinearVelocity;
					break;
				}
				Thread.Sleep(10);
			}
			return;
		}


		#region "EventHandlers"

		public override void Update()
		{
			//prevent multiple update threads to run at once.
			if (m_running) return;
			m_running = true;
			MyPositionAndOrientation position;
			Vector3Wrapper pos;
			Vector3Wrapper velocity;
			
			MyObjectBuilder_FloatingObject tempobject;
			MyObjectBuilder_Ore tempore = new MyObjectBuilder_Ore();
			MyObjectBuilder_InventoryItem tempitem = new MyObjectBuilder_InventoryItem();
			tempore.SetDefaultProperties();
			FloatingObject physicsmeteor;
			
			List<ulong> connectedPlayers = ServerNetworkManager.Instance.GetConnectedPlayers();
			List<Meteor> entityList = SectorObjectManager.Instance.GetTypedInternalData<Meteor>();

			foreach (var sectorObject in entityList)
			{
				if (!sectorObject.IsDisposed)
				{
					position = sectorObject.PositionAndOrientation;
					pos = sectorObject.Position;
					velocity = sectorObject.LinearVelocity;
					velocity = Vector3.Multiply(velocity, velocityFctr);
					sectorObject.Dispose();
					if(!meteoron)
						return;
					if (connectedPlayers.Count > 0 )
					{
						try
						{
							m_ore_fctr = m_gen.NextDouble();

							tempitem = (MyObjectBuilder_InventoryItem)MyObjectBuilder_InventoryItem.CreateNewObject(m_InventoryItemType);
							//Setup the properties of the inventory item
							tempitem.AmountDecimal = Math.Round((decimal)(m_ore_amt * m_ore_fctr));
							tempitem.ItemId = 0;

							tempitem.PhysicalContent = (MyObjectBuilder_PhysicalObject)MyObjectBuilder_PhysicalObject.CreateNewObject(m_OreType);
							tempitem.PhysicalContent.SubtypeName = "Stone";

							tempobject = (MyObjectBuilder_FloatingObject)MyObjectBuilder_FloatingObject.CreateNewObject(m_FloatingObjectType);
							tempobject.Item = tempitem;
							//tempobject.Name = "Meteor Stone";

							physicsmeteor = new FloatingObject(tempobject);
							physicsmeteor.EntityId = physicsmeteor.GenerateEntityId();
							physicsmeteor.PositionAndOrientation = position;
							physicsmeteor.Position = pos;
							physicsmeteor.LinearVelocity = velocity;
							physicsmeteor.MaxLinearVelocity = 104.7F * MaxVelocityFctr;

							SectorObjectManager.Instance.AddEntity(physicsmeteor);
							//workaround for the velocity problem.
							Thread T = new Thread(() => velocityloop(physicsmeteor));
							T.Start();								
							//LogManager.APILog.WriteLineAndConsole("Floating Object Spawned: " + physicsmeteor.EntityId.ToString());
						}
						catch (Exception ex)
						{
							LogManager.APILog.WriteLineAndConsole(ex.ToString());
						}
							
					}
				}
			}
			m_running = false;
		}

		public override void Shutdown()
		{
			m_running = true;
			Thread.Sleep(300);//wait for functions to complete. 
			m_running = false;
			m_maxVelocityFctr = 1F;
			m_velocityFctr = 3F;
			return;
		}


		#endregion



		#endregion
	}
}
