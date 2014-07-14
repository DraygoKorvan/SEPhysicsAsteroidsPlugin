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
			//Console.WriteLine("Update()...");
			//			Type temp_o_type;
			//				MyObjectBuilder_FloatingObject tempobject;
			//			MyObjectBuilder_Ore tempore = new MyObjectBuilder_Ore();
			//			MyObjectBuilder_InventoryItem tempitem = new MyObjectBuilder_InventoryItem();
			//			tempore.SetDefaultProperties();
			//			FloatingObject physicsmeteor;

			List<ulong> connectedPlayers = ServerNetworkManager.Instance.GetConnectedPlayers();
			List<BaseEntity> entityList = SectorObjectManager.Instance.GetTypedInternalData<BaseEntity>();

			foreach (var sectorObject in entityList)
			{
				Type sectorObjectType = sectorObject.GetType();
				if (sectorObjectType == typeof(Meteor))
				{
					if (!sectorObject.IsDisposed)
					{
						Console.WriteLine("Meteor Detected: " + sectorObject.Name + " EntityID:" + sectorObject.EntityId.ToString());
						//deleting meteor
						position = sectorObject.PositionAndOrientation;
						pos = sectorObject.Position;
						velocity = sectorObject.LinearVelocity;
						sectorObject.Dispose();
						//Console.WriteLine("Meteor Properties: " + sectorObject.Name + " EntityID:" + sectorObject.EntityId.ToString() + " Velocity: " + velocity.ToString() + " Position: " + pos.ToString());
						Console.WriteLine("Meteor Deleted: " + sectorObject.Name + " EntityID:" + sectorObject.EntityId.ToString());
						//if there are not enough players connected, abort the shower
						if (connectedPlayers.Count > 0)
						{
							/*try
							{
								temp_o_type = SandboxGameAssemblyWrapper.Instance.GameAssembly.GetType("MyObjectBuilder.FloatingObject");
								Object BackingObject = Activator.CreateInstance(temp_o_type);
								MethodInfo initMethod = BackingObject.GetType().GetMethod("Init", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
								initMethod.Invoke(BackingObject, new object[] { });								
							}
							catch (Exception ex)
							{
								Console.WriteLine("Exception: " + ex.ToString());
							}*/
							/*try
							{
								m_ore_fctr = m_gen.NextDouble();

								tempobject = new MyObjectBuilder_FloatingObject();
								tempobject.PositionAndOrientation = position;
								tempobject.Name = "Stone";
								tempitem.SetDefaultProperties();
								tempitem.AmountDecimal = Math.Round((decimal)(m_ore_amt * m_ore_fctr));
								tempitem.ItemId = 0;
								tempitem.PhysicalContent = new MyObjectBuilder_PhysicalObject();
								tempitem.PhysicalContent.ChangeType(tempore.TypeId, "Stone");
								tempobject.Item = tempitem;
								
								try
								{
									//spawn in new floating object
									physicsmeteor = new FloatingObject(tempobject);
									physicsmeteor.EntityId = physicsmeteor.GenerateEntityId();
									physicsmeteor.LinearVelocity = velocity;
									physicsmeteor.MaxLinearVelocity = 104.7F;
									SectorObjectManager.Instance.AddEntity(physicsmeteor);
									Console.WriteLine("Floating Object Spawned: " + physicsmeteor.EntityId.ToString());
								}
								catch (Exception ex)
								{
									Console.WriteLine("Exception: floatingobject " + ex.ToString());
									throw;
								}
							}
							catch (Exception ex)
							{
								Console.WriteLine("Exception: myobjectbuilder_floatingobject " + ex.ToString());
							}
							*/
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
