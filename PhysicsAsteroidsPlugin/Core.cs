using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Timers;
using System.Reflection;

using Sandbox.Common.ObjectBuilders;

using SEModAPIExtensions.API.Plugin;
using SEModAPIExtensions.API.Plugin.Events;

using SEModAPIInternal.API.Common;
using SEModAPIInternal.API.Entity;
using SEModAPIInternal.API.Entity.Sector.SectorObject;
using SEModAPIInternal.API.Entity.Sector.SectorObject.CubeGrid;
using SEModAPIInternal.API.Entity.Sector.SectorObject.CubeGrid.CubeBlock;
using SEModAPIInternal.API.Server;
using SEModAPIInternal.Support;

using VRageMath;

namespace PhysicsAsteroidsPlugin 
{
	public class Core : PluginBase, ICubeBlockEventHandler
	{
		#region "Attributes"
		double m_ore_amt = 60000;
		double m_ore_fctr = 1;

		Random m_gen;
		#endregion

		#region "Constructors and Initializers"

		public Core()
		{
			Console.WriteLine("PhysicsAsteroidPlugin '" + Id.ToString() + "' constructed!");
		}

		public override void Init()
		{
			m_gen = new Random(3425325);//temp hash
			Console.WriteLine("PhysicsAsteroidPlugin '" + Id.ToString() + "' initialized!");
		}

		#endregion

		#region "Properties"


		#endregion

		#region "Methods"

		#region "EventHandlers"

		public override void Update()
		{
			MyPositionAndOrientation position;
			Vector3 pos;
			Vector3 velocity;


			List<ulong> connectedPlayers = ServerNetworkManager.Instance.GetConnectedPlayers();
			List<BaseEntity> entityList = SectorObjectManager.Instance.GetTypedInternalData<BaseEntity>();

			foreach(var sectorObject in entityList)
			{
				Type sectorObjectType = sectorObject.GetType();
				if (sectorObjectType == typeof(Meteor))
				{
					if (!sectorObject.IsDisposed)
					{
						if (connectedPlayers.Count == 0)
						{
							//if there are not enough players connected, abort the shower
							//Console.WriteLine("Meteor Detected: " + sectorObject.Name + " EntityID:" + sectorObject.EntityId.ToString());
							//deleting meteor
							position = sectorObject.PositionAndOrientation;
							pos = sectorObject.Position;
							velocity = sectorObject.LinearVelocity;
							sectorObject.Dispose();
							//Console.WriteLine("Meteor Properties: " + sectorObject.Name + " EntityID:" + sectorObject.EntityId.ToString() + " Velocity: " + velocity.ToString() + " Position: " + pos.ToString());
							if (SandboxGameAssemblyWrapper.IsDebugging)
							{
								Console.WriteLine("Meteor Deleted: " + sectorObject.Name + " EntityID:" + sectorObject.EntityId.ToString());
								LogManager.APILog.WriteLine("Meteor Deleted: " + sectorObject.Name + " EntityID:" + sectorObject.EntityId.ToString());
							}
							
						}
					}
				}
			}
		}

		public void OnCubeBlockCreated(CubeBlockEntity cubeBlock)
		{
            
		}

		public void OnCubeBlockDeleted(CubeBlockEntity cubeBlock)
		{

		}

		#endregion



		#endregion
	}
}
